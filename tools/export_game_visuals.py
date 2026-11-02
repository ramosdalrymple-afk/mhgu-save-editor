from pathlib import Path
from PIL import Image
import argparse,io
import numpy as np
import struct,json,hashlib
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description='Convert locally extracted MHGU UI textures to an optional editor asset pack.')
parser.add_argument('--input',type=Path,default=root/'artifacts/game-assets')
parser.add_argument('--output',type=Path,default=root/'src/MhguSave.App/Assets/Game')
args=parser.parse_args();source=args.input;dest=args.output;dest.mkdir(parents=True,exist_ok=True)
# MT Framework v160 TEX layout documented by MHGU-Modding and SoupTextures.
# Tegra block-linear GOB addresses are decoded directly; there is no external executable.
def decode_tex(path):
    b=path.read_bytes()
    if len(b)<24 or b[:4]!=b'TEX\0':raise ValueError('Not an MT Framework TEX: '+str(path))
    _,h1,h2,h3,total=struct.unpack_from('<5I',b)
    if h1&4095!=160:raise ValueError('Unsupported TEX version')
    w=(h2>>6)&8191;h=(h2>>19)&8191;fmt=(h3>>8)&255;mips=h2&63
    if not(1<=w<=4096 and 1<=h<=4096)or mips<1 or fmt not in [7,0x13,0x17,0x19,0x1f]:raise ValueError('Unsupported TEX layout')
    offset=20+4*mips+struct.unpack_from('<I',b,20)[0]
    bw,bh,bpp=(1,1,4)if fmt==7 else (4,4,8 if fmt in [0x13,0x19]else 16)
    wb=(w+bw-1)//bw;hb=(h+bh-1)//bh;row=wb*bpp;blockheight=16
    while blockheight>1 and hb<=8*(blockheight//2):blockheight//=2
    y,x=np.indices((hb,row));gobs=(row+63)//64
    address=(y//(8*blockheight))*512*blockheight*gobs+(x//64)*512*blockheight+(y%(8*blockheight)//8)*512+(x%64//32)*256+(y%8//2)*64+(x%32//16)*32+(y%2)*16+x%16
    if offset+int(address.max())>=len(b):raise ValueError('Truncated TEX data')
    raw=np.frombuffer(b[offset:],dtype=np.uint8)[address].tobytes()
    if fmt==7:return Image.frombytes('RGBA',(w,h),raw,'raw','BGRA')
    header=bytearray(128)
    struct.pack_into('<7I',header,0,0x20534444,124,0x81007,h,w,len(raw),0)
    struct.pack_into('<II4s',header,76,32,4,{0x13:b'DXT1',0x17:b'DXT5',0x19:b'BC4U',0x1f:b'BC5U'}[fmt])
    struct.pack_into('<I',header,108,0x1000)
    return Image.open(io.BytesIO(header+raw)).convert('RGBA')
texture_root=source/'nativeNX/eng/GUI/99_texture'
# RGB values and index ordering from MHGU Database's MIT-licensed color.xml.
palette=['#ffffff','#f85858','#70c888','#90b0f8','#f8d058','#b890c0','#98d8f0','#f89858','#e890a0','#c6f29f','#a0a0a0','#e2a626','#5cccff','#639929','#aa3c3c','#4066bb','#803a8e']
atlas=decode_tex(texture_root/'HD_cmn_icon_GSM_NOMIP.tex')
if atlas.size!=(512,1024):raise ValueError('Unsupported common-icon atlas dimensions')
b=(source/'nativeNX/table/itemData.itm').read_bytes();records=struct.unpack_from('<I',b,4)[0]
if b[:4]!=bytes.fromhex('6666aa41')or records!=2991 or len(b)!=8+records*44:raise ValueError('Unsupported item table')
manifest={};metadata=[]
def icon(i,color,name):
 if i<100:x=i%10*48;y=416+i//10*48;size=48
 else:x=i%10*32;y=i//10*32;size=32
 sprite=atlas.crop((x,y,x+size,y+size))
 rgb=tuple(bytes.fromhex(color[1:]));r,g,bl,a=sprite.split()
 sprite=Image.merge('RGBA',(r.point(lambda n:n*rgb[0]//255),g.point(lambda n:n*rgb[1]//255),bl.point(lambda n:n*rgb[2]//255),a))
 sprite.save(dest/name)
for i in range(1,records):
 rec=b[8+i*44:8+(i+1)*44];id=struct.unpack_from('<I',rec)[0];shape,color=rec[16:18]
 filename=f'item-{shape}-{color}.png'
 if filename not in manifest.values():icon(shape,palette[color],filename)
 manifest[f'item:{id}']=filename
 metadata.append({'id':id,'shape':shape,'color':color})
# The common atlas contains a separate 7-column, 24px weapon-class grid.
# These positions follow the visible game symbols (GS, LS, SnS, DB, Hammer, HH,
# Lance / GL, SA, CB, IG, LBG, HBG, Bow), rather than the item-ID grid.
weapon_order=[7,14,8,18,9,19,10,16,15,21,20,13,11,17]
for equipment_type in range(1,25):
 if equipment_type in [1,2,3,4,5,12,23,24]:continue
 filename=f'equipment-{equipment_type}.png'
 if equipment_type in weapon_order:
  slot=weapon_order.index(equipment_type);x=336+slot%7*24;y=144+slot//7*24
  atlas.crop((x,y,x+24,y+24)).save(dest/filename)
 else:
  shape=b[8+353*44+16]if equipment_type==6 else 53
  icon(shape,'#e9eef5',filename)
 manifest[f'equipment:{equipment_type}']=filename
# The credit-roll atlas contains two 1280x720 cinematic stills. Use the lower still,
# without its atlas padding. This is static game artwork, not the saved hunter.
hero=decode_tex(texture_root/'staffroll/HD_MHXXstaffroll_img04_BM_NOMIP.tex').crop((0,720,1280,1440)).convert('RGB');hero.save(dest/'hunter-artwork.jpg',quality=91)
manifest['artwork:hunter']='hunter-artwork.jpg'
decode_tex(texture_root/'HD_load00_BM_NOMIP.tex').crop((0,0,1024,576)).save(dest/'village.png');manifest['artwork:village']='village.png'
(dest/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
provenance={'source':'User-local MHGU.nsp, read-only selective RomFS extraction','sourceTitleId':'0100770008DD8000','texture':'/nativeNX/eng/GUI/99_texture/HD_cmn_icon_GSM_NOMIP.tex','textureSHA256':hashlib.sha256((texture_root/'HD_cmn_icon_GSM_NOMIP.tex').read_bytes()).hexdigest(),'itemMetadata':'/nativeNX/table/itemData.itm (2991 records of 44 bytes; icon at +16, color at +17)','paletteSource':'https://github.com/gatheringhallstudios/MHGenDatabase/blob/develop/app/src/main/res/values/color.xml','artwork':'/nativeNX/eng/GUI/99_texture/staffroll/HD_MHXXstaffroll_img04_BM_NOMIP.tex, crop (0,720,1280,1440)','notes':['Original game sprite silhouettes; item IDs and color indices from game table. Palette RGBs from MHGU Database.','Weapon icons identify weapon classes, not unique 3D weapon models. Armor parts and Palico armor use bundled MHGU Database symbols with separate provenance in Assets/Equipment.','Hunter artwork is a static cinematic still and does not reflect the save character.'],'items':metadata}
(dest/'provenance.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
print(f'{records-1} item mappings, {len(set(manifest.values()))} unique images, {sum(f.stat().st_size for f in dest.iterdir())/1048576:.2f} MiB')
