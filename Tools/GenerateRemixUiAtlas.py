"""Original vector-style Remix UI artwork. Run offline with Pillow; never used at runtime.
Icons use a 24-unit grid and consistent 1.8-unit rounded strokes. No third-party artwork.
The existing original Undo/Redo artwork is reused in the atlas.
"""
from pathlib import Path
import json, math
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parent.parent
NAMES=['Play','Pause','Stop','Record','Previous','Next','Undo','Redo','Volume','Filter','Pitch','Mute','Solo','Loop','Snap','Clear','Reset','Save','Voice','Help','Tools','Room','Spatial','Close','Edit','Live','Check','Surface','Border']
S=512
def icon(name):
    im=Image.new('RGBA',(S,S));d=ImageDraw.Draw(im)
    def pts(p):return [(round(x*S/24),round(y*S/24)) for x,y in p]
    def line(p,w=1.8):
        q=pts(p);width=round(w*S/24);d.line(q,fill='white',width=width,joint='curve')
        r=width/2
        for x,y in q:d.ellipse((x-r,y-r,x+r,y+r),fill='white')
    def poly(p):d.polygon(pts(p),fill='white')
    def rect(box,r=0,fill=None,w=1.8):d.rounded_rectangle(tuple(round(v*S/24) for v in box),radius=round(r*S/24),fill=fill,outline='white' if fill is None else None,width=round(w*S/24))
    def ellipse(box,fill=None,w=1.8):d.ellipse(tuple(round(v*S/24) for v in box),fill=fill,outline='white' if fill is None else None,width=round(w*S/24))
    def arc(box,a,b,w=1.8):d.arc(tuple(round(v*S/24) for v in box),a,b,fill='white',width=round(w*S/24))
    if name in ['Undo','Redo']:
        return Image.open(ROOT/f'Assets/RemixTable/UI/Icons/{name}.png').convert('RGBA').resize((128,128),Image.Resampling.LANCZOS)
    if name=='Play':poly([(8,5),(20,12),(8,19)])
    elif name=='Pause':rect((6,5,10,19),1,'white');rect((14,5,18,19),1,'white')
    elif name=='Stop':rect((6,6,18,18),1,'white')
    elif name=='Record':ellipse((5,5,19,19),'white')
    elif name in ['Previous','Next']:
        poly([(17,5),(7,12),(17,19)] if name=='Previous' else [(7,5),(17,12),(7,19)])
        line([(5,5),(5,19)] if name=='Previous' else [(19,5),(19,19)])
    elif name in ['Volume','Mute']:
        line([(10,8),(6,8),(6,16),(10,16),(15,20),(15,4),(10,8)])
        if name=='Volume':arc((8,4,23,20),300,60);arc((12,7,21,17),300,60)
        else:line([(18,9),(22,15)]);line([(22,9),(18,15)])
    elif name in ['Filter','Tools']:
        for x,y in [(6,9),(12,15),(18,7)]:line([(x,4),(x,20)]);ellipse((x-2,y-2,x+2,y+2),'white')
    elif name=='Pitch':line([(10,17),(10,5),(19,3),(19,15)]);line([(10,8),(19,6)]);ellipse((4,15,10,20),'white');ellipse((13,13,19,18),'white')
    elif name=='Solo':arc((4,4,20,22),180,360);rect((3,11,7,20),1,'white');rect((17,11,21,20),1,'white')
    elif name=='Loop':line([(5,9),(5,6),(19,6),(21,9)]);line([(17,3),(20,6),(17,9)]);line([(19,15),(19,18),(5,18),(3,15)]);line([(7,15),(4,18),(7,21)])
    elif name=='Snap':arc((4,3,20,20),0,180,3);line([(5.5,11),(5.5,5)],3);line([(18.5,11),(18.5,5)],3);line([(4,7),(7,7)],1);line([(17,7),(20,7)],1)
    elif name=='Clear':line([(4,14),(13,4),(21,11),(12,20),(8,20),(4,16),(4,14)]);line([(8,10),(16,16)]);line([(11,20),(21,20)])
    elif name=='Reset':arc((4,4,20,20),210,510);line([(3,5),(3,11),(9,11)])
    elif name=='Save':line([(5,3),(18,3),(21,6),(21,21),(3,21),(3,3),(5,3)]);rect((7,3,16,10));rect((7,14,17,21))
    elif name=='Voice':rect((9,3,15,15),3);arc((6,6,18,19),0,180);line([(6,10),(6,13)]);line([(18,10),(18,13)]);line([(12,19),(12,22)]);line([(8,22),(16,22)])
    elif name=='Help':ellipse((3,3,21,21));arc((8,6,16,13),180,450);line([(12,12),(12,14)]);ellipse((11,17,13,19),'white')
    elif name=='Room':line([(3,10),(12,3),(21,10),(21,21),(3,21),(3,10)]);line([(7,16),(10,13),(13,16),(17,11),(20,14)])
    elif name=='Spatial':ellipse((9,9,15,15),'white');ellipse((3,10,7,14));ellipse((17,10,21,14));arc((7,3,17,21),220,320);arc((7,3,17,21),40,140)
    elif name=='Close':line([(6,6),(18,18)]);line([(18,6),(6,18)])
    elif name=='Edit':line([(5,15),(15,5),(20,10),(10,20),(4,21),(5,15)]);line([(12,8),(17,13)])
    elif name=='Live':poly([(5,6),(14,12),(5,18)]);line([(17,7),(17,17)]);arc((13,4,23,20),300,60)
    elif name=='Check':line([(5,12),(10,17),(20,7)],2.4)
    elif name in ['Surface','Border']:
        rect((.25,.25,23.75,23.75),5,'white' if name=='Surface' else None,.55)
    else:raise ValueError(name)
    return im.resize((128,128),Image.Resampling.LANCZOS)

if __name__=='__main__':
    atlas=Image.new('RGBA',(1024,512))
    for i,name in enumerate(NAMES):atlas.alpha_composite(icon(name),((i%8)*128,(i//8)*128))
    target=ROOT/'Assets/Resources/RemixUiAtlas.png';atlas.save(target,optimize=True)
    print(f'Generated {len(NAMES)} original/reused icons: {target}')
