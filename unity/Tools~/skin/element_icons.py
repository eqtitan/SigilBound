"""Gemini element emblems -> 128px icons in the original painted ring (Resources/Skin/icons)."""
from PIL import Image, ImageDraw, ImageEnhance
import numpy as np, sys
def inner_r(img, bright):
    a=np.asarray(img.convert("RGBA")).astype(int); best=[]; c=img.width//2
    for ang in range(0,360,15):
        t=np.radians(ang); seen=False
        for rr in range(c-1,10,-1):
            x=int(c+rr*np.cos(t)); y=int(c+rr*np.sin(t)); p=a[y,x]
            if p[3]<200: continue
            L=p[:3].sum()
            if L>bright: seen=True
            elif seen and L<200: best.append(rr); break
    return int(np.median(best))
def circ(size,r,S=4):
    c=size/2; m=Image.new("L",(size*S,size*S),0); ImageDraw.Draw(m).ellipse([(c-r)*S,(c-r)*S,(c+r)*S,(c+r)*S],fill=255); return m.resize((size,size),Image.LANCZOS)
ref=Image.open("ref_flame.png").convert("RGBA"); RI=inner_r(ref,330)
out={}
for e in sys.argv[1:]:
    im=Image.open(f"{e}_1.png").convert("RGB"); a=np.asarray(im).astype(int)
    bg=np.median(np.concatenate([a[:8,:8].reshape(-1,3),a[-8:,-8:].reshape(-1,3)]),0)
    ys,xs=np.where(np.abs(a-bg).sum(2)>60); cx,cy=(xs.min()+xs.max())//2,(ys.min()+ys.max())//2; r=max(xs.max()-xs.min(),ys.max()-ys.min())//2
    sq=im.crop((cx-r,cy-r,cx+r,cy+r)).convert("RGBA")
    ni=inner_r(sq.resize((256,256)),420)*(2*r)/256          # new inner disc radius in source px
    k=RI/ni                                                 # scale so new inner disc == ref inner disc
    S=round(2*r*k); big=sq.resize((S,S),Image.LANCZOS)
    emb=Image.new("RGBA",(128,128),(0,0,0,0)); emb.alpha_composite(big,(64-S//2,64-S//2))
    emb=ImageEnhance.Brightness(emb.convert("RGB")).enhance(0.92).convert("RGBA")
    fin=ref.copy(); fin.paste(emb,(0,0),circ(128,RI+1)); fin.save(f"{e}.png"); out[e]=fin
tide=Image.open("../wt-merge/unity/Assets/Resources/Skin/icons/tide.png").convert("RGBA")
row=Image.new("RGBA",((2+len(out))*140,140),(40,30,50,255))
for i,ic in enumerate([ref,tide]+list(out.values())): row.alpha_composite(ic,(i*140+6,6))
row.save("elements_row.png"); print("ref inner",RI)
