"""Extract icon metadata from the user's own western MHGU NSO; never execute it."""
import hashlib, struct
import lz4.block
from capstone import Cs, CS_ARCH_ARM, CS_MODE_ARM
from capstone.arm import ARM_OP_IMM, ARM_OP_MEM, ARM_OP_REG, ARM_REG_R0, ARM_REG_R1, ARM_REG_R2, ARM_REG_R3


def read_nso(path):
    data = path.read_bytes()
    if data[:4] != b'NSO0' or len(data) < 256:
        raise ValueError('Expected the original MHGU exefs/main NSO')
    flags = struct.unpack_from('<I', data, 12)[0]
    segments = []
    for index in range(3):
        offset, address, size = struct.unpack_from('<III', data, 16 + 16 * index)
        if size > 64 * 1024 * 1024 or address + size > 128 * 1024 * 1024:
            raise ValueError('Unexpected NSO segment bounds')
        packed_size = struct.unpack_from('<I', data, 96 + 4 * index)[0] if flags & (1 << index) else size
        packed = data[offset:offset + packed_size]
        raw = lz4.block.decompress(packed, uncompressed_size=size) if flags & (1 << index) else packed
        if len(raw) != size:
            raise ValueError('NSO segment size mismatch')
        if flags & (1 << (3 + index)) and hashlib.sha256(raw).digest() != data[160 + 32 * index:192 + 32 * index]:
            raise ValueError('NSO segment hash mismatch')
        segments.append((address, raw))
    memory = bytearray(max(address + len(raw) for address, raw in segments))
    for address, raw in segments:
        memory[address:address + len(raw)] = raw
    return memory


def read_palettes(memory):
    # Recover constants from the game's ARM32 icon-color constructor. This evaluates
    # immediate arithmetic only; it neither runs the executable nor follows calls.
    disassembler = Cs(CS_ARCH_ARM, CS_MODE_ARM)
    disassembler.detail = True
    registers, colors, rarity = {}, {}, {}
    def value(operand):
        return operand.imm if operand.type == ARM_OP_IMM else registers.get(operand.reg) if operand.type == ARM_OP_REG else None
    for ins in disassembler.disasm(bytes(memory[0x562060:0x562638]), 0x562060):
        ops = ins.operands
        result = None
        if ins.mnemonic in ('mov', 'movw', 'mvn') and len(ops) == 2:
            result = value(ops[1])
            if result is not None and ins.mnemonic == 'mvn': result = ~result
        elif ins.mnemonic == 'movt' and registers.get(ops[0].reg) is not None:
            result = (registers[ops[0].reg] & 65535) | (ops[1].imm << 16)
        elif ins.mnemonic in ('add', 'adds', 'sub') and len(ops) == 3:
            a, b = value(ops[1]), value(ops[2])
            if a is not None and b is not None: result = a - b if ins.mnemonic == 'sub' else a + b
        elif ins.address == 0x5625d4 and ins.mnemonic == 'ldr':
            # Track the rarity-array base as a relative offset, without dereferencing it.
            result = 0
        if 0x562514 <= ins.address <= 0x562594 and ins.mnemonic == 'str' and len(ops) == 2:
            mem = ops[1]
            if mem.type == ARM_OP_MEM and ins.reg_name(mem.mem.base) == 'r0':
                colors[mem.mem.disp // 4] = value(ops[0])
        if 0x5625d8 <= ins.address <= 0x562634:
            if ins.mnemonic == 'str' and ops[1].type == ARM_OP_MEM and ins.reg_name(ops[1].mem.base) == 'r0':
                rarity[ops[1].mem.disp // 4] = value(ops[0])
            elif ins.mnemonic == 'stm' and ins.reg_name(ops[0].reg) == 'r0':
                offset = registers.get(ops[0].reg)
                if offset is not None:
                    for i, operand in enumerate(ops[1:]): rarity[offset // 4 + i] = value(operand)
        _, written = ins.regs_access()
        for reg in written: registers.pop(reg, None)
        if result is not None and ops[0].type == ARM_OP_REG: registers[ops[0].reg] = result & 0xffffffff
        if ins.mnemonic == 'bl':
            for reg in (ARM_REG_R0, ARM_REG_R1, ARM_REG_R2, ARM_REG_R3): registers.pop(reg, None)
    if set(colors) != set(range(17)) or any(color is None or color >> 24 != 255 for color in colors.values()):
        raise ValueError('Unsupported icon-color constructor; refusing guessed colors')
    if set(rarity) != set(range(11)) or any(c is None or c >> 24 != 255 for c in rarity.values()):
        raise ValueError('Unsupported rarity-color constructor; refusing guessed colors')
    return tuple([tuple((table[i] >> shift) & 255 for shift in (0, 8, 16)) for i in range(len(table))] for table in (colors, rarity))


def read_palette(memory):
    return read_palettes(memory)[0]


def read_icon_tables(path):
    memory = read_nso(path)
    monsters = list(struct.unpack_from('<137I', memory, 0x162e7f8))
    equipment = list(memory[0x162e7dc:0x162e7dc + 25])
    extra = [tuple(2 * n for n in struct.unpack_from('<II', memory, 0x162e1a0 + 8 * i)) for i in range(1, 13)]
    if monsters[:105] != list(range(105)) or max(monsters) >= 147 or len(equipment) != 25 or max(equipment) > 106:
        raise ValueError('The executable icon tables do not match the supported MHGU layout')
    if extra[0] != (240, 848) or any(x + 48 > 512 or y + 48 > 1024 for x, y in extra):
        raise ValueError('Unsupported extended item-icon coordinates')
    return monsters, equipment, extra, read_palette(memory)
