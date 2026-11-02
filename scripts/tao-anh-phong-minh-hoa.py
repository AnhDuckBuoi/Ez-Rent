# Vẽ ảnh minh họa phòng trọ (flat illustration) cho EZ-Rent. Vẽ ở 2x rồi thu nhỏ để khử răng cưa.
from PIL import Image, ImageDraw, ImageFilter
import random, os
W, H, S = 800, 600, 2
OUT = '/home/claude/roomgen/out'; os.makedirs(OUT, exist_ok=True)

def hexc(h, a=255):
    if isinstance(h, tuple): return h if len(h) == 4 else h + (a,)
    h = h.lstrip('#')
    if len(h) == 3: h = ''.join(ch*2 for ch in h)
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4)) + (a,)
def shade(c, f): return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (c[3] if len(c) > 3 else 255,)

class Canvas:
    def __init__(s, bg='#f3efe8'):
        s.im = Image.new('RGBA', (W*S, H*S), hexc(bg)); s.d = ImageDraw.Draw(s.im, 'RGBA')
    def P(s, pts): return [(x*S, y*S) for x, y in pts]
    def poly(s, pts, fill, outline=None): s.d.polygon(s.P(pts), fill=hexc(fill) if isinstance(fill, str) else fill, outline=outline)
    def rect(s, x0, y0, x1, y1, fill, r=0):
        f = hexc(fill) if isinstance(fill, str) else fill
        if r: s.d.rounded_rectangle([x0*S, y0*S, x1*S, y1*S], radius=r*S, fill=f)
        else: s.d.rectangle([x0*S, y0*S, x1*S, y1*S], fill=f)
    def ell(s, x0, y0, x1, y1, fill): s.d.ellipse([x0*S, y0*S, x1*S, y1*S], fill=hexc(fill) if isinstance(fill, str) else fill)
    def line(s, pts, fill, w=2): s.d.line(s.P(pts), fill=hexc(fill) if isinstance(fill, str) else fill, width=int(w*S))
    def shadow(s, x0, y0, x1, y1, alpha=60, blur=14):
        layer = Image.new('RGBA', s.im.size, (0, 0, 0, 0)); ImageDraw.Draw(layer).ellipse([x0*S, y0*S, x1*S, y1*S], fill=(40, 30, 20, alpha))
        s.im = Image.alpha_composite(s.im, layer.filter(ImageFilter.GaussianBlur(blur*S))); s.d = ImageDraw.Draw(s.im, 'RGBA')
    def light(s):  # ánh sáng dịu từ cửa sổ, vignette nhẹ
        g = Image.new('L', (W, H), 0); gd = ImageDraw.Draw(g)
        for i in range(60): gd.ellipse([-200+i*4, -260+i*3, W+200-i*4, H+120-i*2], outline=int(i*1.6))
        g = g.resize(s.im.size).filter(ImageFilter.GaussianBlur(40))
        dark = Image.new('RGBA', s.im.size, (30, 22, 10, 0)); dark.putalpha(g.point(lambda v: max(0, 34 - v)))
        s.im = Image.alpha_composite(s.im, dark); s.d = ImageDraw.Draw(s.im, 'RGBA')
    def save(s, name):
        s.light(); s.im.convert('RGB').resize((W, H), Image.LANCZOS).save(f'{OUT}/{name}.jpg', quality=86, optimize=True, progressive=True)

# Hộp phòng phối cảnh một điểm tụ: tường sau, sàn, trần, hai tường bên
BX0, BY0, BX1, BY1 = 190, 120, 610, 400
def room(c, wall, side, floor, ceil='#fbf8f3', boards=True):
    c.poly([(0, 0), (W, 0), (BX1, BY0), (BX0, BY0)], ceil)
    c.poly([(0, 0), (BX0, BY0), (BX0, BY1), (0, H)], side)
    c.poly([(W, 0), (BX1, BY0), (BX1, BY1), (W, H)], shade(hexc(side), .94))
    c.rect(BX0, BY0, BX1, BY1, wall)
    c.poly([(BX0, BY1), (BX1, BY1), (W, H), (0, H)], floor)
    if boards:
        for i in range(1, 9):
            x = BX0 + (BX1-BX0)*i/9; c.line([(x, BY1), (W*i/9, H)], shade(hexc(floor), .9), 1.2)
    c.line([(BX0, BY1), (BX1, BY1)], shade(hexc(wall), .8), 2)

def window(c, x0, y0, x1, y1, sky=('#bfe3f7', '#e9f6fd'), frame='#ffffff', curtain=None):
    c.rect(x0-8, y0-8, x1+8, y1+8, frame)
    for k in range(int(y1-y0)):
        t = k/(y1-y0); a, b = hexc(sky[0]), hexc(sky[1])
        c.rect(x0, y0+k, x1, y0+k+1, tuple(int(a[j]+(b[j]-a[j])*t) for j in range(3))+(255,))
    c.poly([(x0, y1), (x0+(x1-x0)*.25, y0+(y1-y0)*.55), (x0+(x1-x0)*.45, y1)], '#9cc7a4')
    c.rect(x0+(x1-x0)*.55, y0+(y1-y0)*.35, x0+(x1-x0)*.72, y1, '#d4dde6'); c.rect(x0+(x1-x0)*.74, y0+(y1-y0)*.5, x0+(x1-x0)*.9, y1, '#c3ced9')
    c.rect((x0+x1)/2-3, y0, (x0+x1)/2+3, y1, frame); c.rect(x0, (y0+y1)/2-3, x1, (y0+y1)/2+3, frame)
    if curtain:
        c.rect(x0-26, y0-16, x0+10, y1+30, curtain, 6); c.rect(x1-10, y0-16, x1+26, y1+30, curtain, 6)

def plant(c, x, y, s=1.0):
    c.rect(x-14*s, y-26*s, x+14*s, y, '#c98b5b', 4)
    for dx, dy, r in [(-18, -60, 20), (0, -76, 22), (18, -58, 20), (-6, -46, 16), (10, -44, 16)]:
        c.ell(x+(dx-r)*s, y+(dy-r)*s, x+(dx+r)*s, y+(dy+r)*s, '#5f9e6e' if r > 18 else '#4f8a5f')

def bed(c, x0, y0, x1, y1, blanket, pillow='#ffffff', frame='#b88a5e'):
    c.shadow(x0-10, y1-30, x1+30, y1+30, 70)
    c.rect(x0, y0-70, x0+18, y1, shade(hexc(frame), .85), 4)          # đầu giường
    c.rect(x0+10, y0, x1, y1, frame, 8)
    c.rect(x0+14, y0-12, x1-4, y0+26, '#f7f4ee', 10)                   # nệm
    c.rect(x0+22, y0-34, x0+110, y0+4, pillow, 14)
    c.rect(x0+80, y0+2, x1-4, y1-14, blanket, 12)
    c.rect(x0+80, y0+2, x1-4, y0+18, shade(hexc(blanket), 1.12), 8)

def wardrobe(c, x0, y0, x1, y1, col='#e7d7c1'):
    c.shadow(x0, y1-10, x1+20, y1+20, 50); c.rect(x0, y0, x1, y1, col, 4)
    c.line([((x0+x1)/2, y0+6), ((x0+x1)/2, y1-6)], shade(hexc(col), .8), 2)
    c.rect((x0+x1)/2-12, (y0+y1)/2-14, (x0+x1)/2-8, (y0+y1)/2+14, '#8b6b4a', 2); c.rect((x0+x1)/2+8, (y0+y1)/2-14, (x0+x1)/2+12, (y0+y1)/2+14, '#8b6b4a', 2)

def desk(c, x0, y, x1, col='#c69c6d'):
    c.shadow(x0, y+60, x1+10, y+90, 45); c.rect(x0, y, x1, y+12, col, 3)
    c.rect(x0+8, y+12, x0+18, y+80, shade(hexc(col), .8)); c.rect(x1-18, y+12, x1-8, y+80, shade(hexc(col), .8))
    c.rect(x0+30, y-46, x0+92, y-6, '#2f3b4a', 4); c.rect(x0+56, y-6, x0+66, y, '#2f3b4a')
    c.rect(x1-50, y-34, x1-44, y, '#555'); c.poly([(x1-66, y-34), (x1-28, y-34), (x1-38, y-58), (x1-56, y-58)], '#f2c46d')

def rug(c, cx, cy, rx, ry, col): c.ell(cx-rx, cy-ry, cx+rx, cy+ry, col); c.ell(cx-rx+14, cy-ry+8, cx+rx-14, cy+ry-8, shade(hexc(col), 1.08))

def ac(c, x0, y0, w=150):
    c.rect(x0, y0, x0+w, y0+44, '#fdfdfd', 10); c.rect(x0+10, y0+30, x0+w-10, y0+36, '#d9dde2', 3); c.ell(x0+w-24, y0+10, x0+w-16, y0+18, '#6fcf97')

PALETTES = [('#efe6da', '#e3d6c4', '#cfa77a', '#7aa2c8', '#d9cbb8'), ('#e6eef0', '#d7e3e6', '#bfa07f', '#e7a77f', '#cfd9dc'),
            ('#f2ece4', '#e6dccf', '#a9825e', '#8fb59a', '#dccfbd'), ('#ece9f1', '#dedaea', '#c8a27c', '#c9a0c4', '#d8d3e5')]

def bedroom(name, p, loft=False):
    wall, side, floor, blanket, rugc = p; c = Canvas(); room(c, wall, side, floor)
    window(c, 300, 160, 480, 300, curtain='#f4efe6')
    if loft:  # gác xép: sàn gác + lan can + thang
        c.rect(0, 92, 330, 112, '#b88a5e'); c.rect(0, 112, 330, 122, shade(hexc('#b88a5e'), .8))
        for x in range(20, 320, 34): c.rect(x, 50, x+6, 92, '#9c7550')
        c.rect(0, 46, 330, 54, '#9c7550'); c.rect(40, 70, 230, 92, '#f7f4ee', 8); c.rect(60, 58, 120, 80, '#ffffff', 10)
        for k in range(7): c.rect(352, 122+k*44, 422, 128+k*44, '#a07a54')
        c.rect(350, 112, 358, 430, '#8b6b4a'); c.rect(416, 112, 424, 430, '#8b6b4a')
        desk(c, 470, 330, 640); bed(c, 90, 420, 330, 500, blanket)
    else:
        wardrobe(c, 560, 150, 700, 430); bed(c, 150, 380, 470, 470, blanket); desk(c, 470, 330, 560) if False else None
    rug(c, 400, 540, 230, 40, rugc); plant(c, 120, 440, .9); ac(c, 520, 128)
    c.save(name)

def living(name, p):
    wall, side, floor, accent, rugc = p; c = Canvas(); room(c, wall, side, floor)
    window(c, 420, 150, 580, 290, curtain='#efe7dc')
    c.rect(230, 170, 380, 260, '#2c3440', 6); c.rect(238, 178, 372, 252, '#41556b', 4); c.rect(220, 300, 390, 330, '#b88a5e', 4)
    rug(c, 400, 520, 250, 50, rugc); c.shadow(140, 460, 520, 520, 70)
    c.rect(150, 400, 500, 480, accent, 18); c.rect(150, 380, 500, 420, shade(hexc(accent), 1.1), 16)
    c.rect(130, 400, 175, 490, shade(hexc(accent), .9), 14); c.rect(475, 400, 520, 490, shade(hexc(accent), .9), 14)
    c.rect(200, 390, 270, 430, '#f6f1e8', 12); c.rect(380, 390, 450, 430, '#f6d9a8', 12)
    c.shadow(300, 525, 520, 560, 50); c.rect(310, 500, 500, 520, '#c69c6d', 6); plant(c, 640, 470, 1.1); ac(c, 230, 128)
    c.save(name)

def kitchen(name, p):
    wall, side, floor, accent, _ = p; c = Canvas(); room(c, '#f4f1ec', side, floor)
    for y in range(240, 330, 18):
        for x in range(BX0, BX1, 30): c.rect(x+1, y+1, x+29, y+17, '#e9eef2')
    c.rect(BX0, 140, BX1, 220, accent, 4)
    for x in range(BX0, BX1, 105): c.line([(x, 140), (x, 220)], shade(hexc(accent), .85), 2)
    c.rect(BX0, 330, BX1, 400, shade(hexc(accent), .95), 4); c.rect(BX0-6, 322, BX1+6, 334, '#d8d2c8', 2)
    c.rect(300, 300, 380, 324, '#3a3f45', 4); c.ell(310, 304, 336, 320, '#55595e'); c.ell(344, 304, 370, 320, '#55595e')
    c.rect(450, 296, 540, 322, '#c9cdd2', 6); c.rect(560, 180, 640, 400, '#e9edf0', 6); c.line([(566, 260), (634, 260)], '#c9cfd4', 2)
    c.shadow(250, 520, 560, 560, 50); c.rect(270, 430, 540, 450, '#c69c6d', 6)
    for x in (290, 500): c.rect(x, 450, x+12, 540, '#9c7550')
    plant(c, 610, 470, .8); c.save(name)

def bathroom(name, p):
    _, _, _, accent, _ = p; c = Canvas(); room(c, '#eef3f5', '#dde7eb', '#cfd8dc', boards=False)
    for y in range(BY0, BY1, 26):
        for x in range(BX0, BX1, 26): c.rect(x+1, y+1, x+25, y+25, '#e4edf0')
    for i in range(1, 7): x = BX0+(BX1-BX0)*i/7; c.line([(x, BY1), (W*i/7, H)], '#bfc9cd', 1.2)
    c.rect(220, 160, 350, 400, '#e6f2f7'); c.rect(222, 162, 348, 398, (200, 230, 240, 120)); c.line([(350, 160), (350, 400)], '#b8c4c9', 3)
    c.rect(270, 150, 300, 158, '#9aa5ab'); c.line([(285, 158), (285, 190)], '#9aa5ab', 3); c.ell(270, 186, 300, 200, '#9aa5ab')
    c.rect(430, 170, 540, 260, '#dfe9ee', 10); c.rect(436, 176, 534, 254, '#cfe0e8', 8)
    c.rect(420, 290, 550, 330, '#ffffff', 10); c.ell(445, 296, 525, 318, '#e3eaed'); c.rect(440, 330, 530, 400, accent, 6)
    c.shadow(560, 470, 700, 520, 50); c.rect(590, 380, 680, 440, '#ffffff', 12); c.ell(580, 420, 690, 500, '#ffffff'); c.ell(596, 430, 674, 486, '#eef3f5')
    c.rect(110, 300, 140, 420, '#f3d39b', 10); c.save(name)

def dorm(name, p):
    wall, side, floor, blanket, rugc = p; c = Canvas(); room(c, wall, side, floor)
    window(c, 330, 150, 470, 270, curtain='#f1ece3')
    def bunk(x0, x1, col):
        for x in (x0, x1-12): c.rect(x, 170, x+12, 520, '#7f8c99', 3)
        for y, b in ((260, col), (440, shade(hexc(col), .92))):
            c.rect(x0+6, y, x1-6, y+22, '#9aa5b1', 3); c.rect(x0+12, y-18, x1-12, y+4, '#f7f4ee', 8); c.rect(x0+60, y-16, x1-14, y+4, b, 8); c.rect(x0+16, y-30, x0+60, y-10, '#fff', 8)
        for k in range(5): c.rect(x1+2, 290+k*32, x1+34, 296+k*32, '#7f8c99')
        c.rect(x1+2, 270, x1+8, 440, '#7f8c99'); c.rect(x1+28, 270, x1+34, 440, '#7f8c99')
    c.shadow(40, 500, 330, 540, 60); bunk(40, 270, blanket); c.shadow(480, 500, 760, 540, 60); bunk(510, 720, shade(hexc(blanket), 1.15))
    for x in (300, 360, 420): c.rect(x, 330, x+56, 470, '#d9cbb8', 4); c.ell(x+40, 395, x+48, 405, '#8b6b4a')
    c.save(name)

def balcony(name, p):
    c = Canvas('#cfe8f6')
    for k in range(320): t = k/320; c.rect(0, k, W, k+1, (int(150+90*t), int(200+40*t), int(235+15*t), 255))
    for i, (x, w, h) in enumerate([(30, 90, 210), (130, 70, 150), (210, 110, 260), (330, 80, 190), (420, 120, 240), (550, 90, 170), (650, 120, 230)]):
        c.rect(x, 330-h, x+w, 330, '#9fb3c8' if i % 2 else '#b3c3d4')
        for yy in range(330-h+16, 320, 22):
            for xx in range(x+10, x+w-10, 20): c.rect(xx, yy, xx+8, yy+10, '#dfe8f0')
    c.rect(0, 330, W, H, '#c9b49a'); c.poly([(0, 330), (W, 330), (W, 360), (0, 360)], '#b49b7c')
    for x in range(0, W, 46): c.rect(x, 360, x+8, 520, '#4c5560')
    c.rect(0, 352, W, 366, '#3f4852'); c.rect(0, 510, W, 522, '#3f4852')
    plant(c, 110, 560, 1.2); plant(c, 690, 560, 1.0); c.rect(300, 440, 500, 456, '#b88a5e', 4); c.rect(320, 456, 332, 540, '#9c7550'); c.rect(468, 456, 480, 540, '#9c7550')
    c.rect(340, 410, 380, 440, '#f6f1e8', 6); c.save(name)

def house(name, p):
    _, _, _, accent, _ = p; c = Canvas('#dbeef9')
    for k in range(H): t = k/H; c.rect(0, k, W, k+1, (int(190+50*t), int(225+20*t), int(245+5*t), 255))
    c.rect(0, 500, W, H, '#c7c2b8'); c.rect(0, 492, W, 504, '#b1aba0')
    c.shadow(220, 470, 600, 520, 60); c.rect(240, 110, 560, 500, '#f2ede4', 2); c.poly([(226, 112), (574, 112), (540, 80), (260, 80)], '#8a6a52')
    for fy in (130, 250, 370):
        c.rect(270, fy+20, 400, fy+100, '#d6e6ef'); c.rect(274, fy+24, 396, fy+96, '#c4dbe8'); c.line([(335, fy+20), (335, fy+100)], '#ffffff', 3)
        c.rect(430, fy+20, 530, fy+100, accent, 3) if fy == 370 else c.rect(430, fy+20, 530, fy+100, '#c4dbe8')
        if fy < 370: c.rect(250, fy+104, 550, fy+112, '#5c6670'); [c.rect(x, fy+80, x+5, fy+104, '#5c6670') for x in range(254, 548, 20)]
    c.rect(440, 400, 520, 500, '#7a5a42', 3); c.ell(505, 448, 513, 456, '#f2c46d')
    plant(c, 210, 500, 1.1); plant(c, 600, 500, 1.0); c.rect(80, 430, 130, 500, '#5d7a8c', 8); c.ell(70, 470, 100, 500, '#333'); c.ell(110, 470, 140, 500, '#333')
    c.save(name)

def parking(name, p):
    c = Canvas('#e9e6e1'); room(c, '#e6e1d8', '#d9d3c8', '#bdb7ad', boards=False)
    for i, col in enumerate(['#c0392b', '#2c3e50', '#2980b9', '#7f8c8d']):
        x = 190 + i*110; c.shadow(x-10, 440, x+110, 470, 60)
        c.ell(x, 420, x+40, 460, '#2b2b2b'); c.ell(x+60, 420, x+100, 460, '#2b2b2b'); c.ell(x+8, 428, x+32, 452, '#9a9a9a'); c.ell(x+68, 428, x+92, 452, '#9a9a9a')
        c.poly([(x+18, 432), (x+82, 432), (x+70, 392), (x+30, 392)], col); c.rect(x+28, 380, x+62, 392, '#333', 4); c.rect(x+70, 372, x+76, 400, '#555')
    c.rect(BX0+10, 150, BX0+170, 220, '#ffffff', 6); c.rect(BX0+20, 160, BX0+60, 210, '#2e86de', 4)
    c.rect(BX1-140, 150, BX1-20, 190, '#3a3f45', 6); c.ell(BX1-120, 158, BX1-100, 182, '#e74c3c')
    c.save(name)

def ac_leak(name):
    c = Canvas(); room(c, '#eef0ee', '#e1e4e1', '#c9b8a3'); c.rect(250, 150, 550, 230, '#fdfdfd', 18); c.rect(270, 200, 530, 212, '#d9dde2', 4); c.ell(510, 170, 522, 182, '#6fcf97')
    for k, x in enumerate((300, 360, 420, 480)):
        for j in range(3): c.ell(x-6, 240+j*60+k*14, x+6, 258+j*60+k*14, '#7cc4f2')
    c.ell(250, 470, 560, 520, (124, 196, 242, 140)); c.save(name)

jobs = []
for i, p in enumerate(PALETTES): jobs.append((bedroom, f'phong-ngu-{i+1}', p))
for i, p in enumerate(PALETTES[:3]): jobs += [(lambda n, pp: bedroom(n, pp, loft=True), f'studio-gac-{i+1}', p), (living, f'phong-khach-{i+1}', p),
    (kitchen, f'bep-{i+1}', p), (bathroom, f'nha-ve-sinh-{i+1}', p), (dorm, f'ky-tuc-xa-{i+1}', p), (balcony, f'ban-cong-{i+1}', p), (house, f'nha-nguyen-can-{i+1}', p)]
for i, p in enumerate(PALETTES[:2]): jobs.append((parking, f'cho-de-xe-{i+1}', p))
for f, n, p in jobs: f(n, p)
ac_leak('su-co-dieu-hoa')
print(len(os.listdir(OUT)), 'ảnh')
