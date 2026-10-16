"""
The pictures for the café's wave-1 pieces (14 Oct 2026), drawn with Pillow (no Blender): cafe_repairside.py maps them.
Every word on them is PLACEHOLDER COPY for Content (names, notes, the lender, the amounts); the lender is fictional
(Pennywhistle Mortgage Co.), as the Bible's brand rule asks. Fonts: Caveat (OFL) for hands, Inter (OFL) for print.

  LI_Tags.png         1024 x 64: eight manila repair tags in a row (128 x 64 each; the seventh is the last used), a
                      punched hole at the left, a name in pen
  LI_Whiteboard.png   1024 x 683: "THIS WEEK" in marker, six jobs and an order
  LI_Mortgage.png     1024 x 1325 (US letter): a mortgage statement, the past-due line circled in red pen
  LI_FinalNotice.png  1280 x 1024: the final notice's page (u 0..0.62) and its envelope standing on end (u 0.62..1) with a
                      red rubber stamp

Run:  python3 cafe_wave1_textures.py [<texture folder>]   (default Assets/Art/Textures/Cafe/ in the repository)
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, 'fonts')
IN_REPO = os.path.basename(HERE) == 'Blender' and os.path.basename(os.path.dirname(HERE)) == 'Tools'
DEFAULT_OUT = os.path.join(HERE, '..', '..', 'Assets', 'Art', 'Textures', 'Cafe') if IN_REPO else os.path.join(HERE, 'textures')


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def tags(path):
    im = Image.new('RGB', (1024, 64), (0xE8, 0xD9, 0xA8))
    d = ImageDraw.Draw(im)
    names = ['Okafor - radio', 'D. Reyes - fan', 'Priya - phones', 'Tom - pad', 'Hal - kettle', 'Mrs Lin - lamp', 'Sam - speaker', '']
    hand = font('Caveat-Bold.ttf', 30)
    for k, n in enumerate(names):
        x0 = k * 128
        d.rectangle([x0, 0, x0 + 127, 63], outline=(0xC9, 0xB7, 0x84), width=2)
        d.ellipse([x0 + 8, 26, x0 + 18, 36], fill=(0xB8, 0xA6, 0x74))
        if n:
            d.text((x0 + 24, 32), n, font=hand, fill=(0x2A, 0x2E, 0x4A), anchor='lm')
    im.save(path)


def whiteboard(path):
    im = Image.new('RGB', (1024, 683), (0xF4, 0xF3, 0xEF))
    d = ImageDraw.Draw(im)
    big, hand = font('Caveat-Bold.ttf', 64), font('Caveat-Bold.ttf', 44)
    d.text((60, 40), 'THIS WEEK', font=big, fill=(0x1E, 0x2B, 0x5A))
    d.line([(60, 112), (380, 108)], fill=(0x1E, 0x2B, 0x5A), width=4)
    lines = [('radio - Okafor', 'READY', (0x2E, 0x7D, 0x5B)), ('fan - Reyes: new cap', '', None),
             ('kettle - Hal: element?', '', None), ('lamp - Mrs Lin', 'READY', (0x2E, 0x7D, 0x5B)),
             ('speaker - Sam: cone', '', None), ('pad - Tom: battery', 'wait parts', (0xB5, 0x40, 0x2F))]
    y = 150
    for text, mark, col in lines:
        d.text((70, y), text, font=hand, fill=(0x22, 0x22, 0x26))
        if mark:
            d.text((560, y), mark, font=hand, fill=col)
        y += 66
    d.text((640, 560), 'ORDER: screen glue,', font=hand, fill=(0xB5, 0x40, 0x2F))
    d.text((640, 604), '2 mm screws', font=hand, fill=(0xB5, 0x40, 0x2F))
    im.save(path)


def letter_page(d, w, title, rows, small, body, boldf, circle_row=None):
    d.text((70, 60), 'Pennywhistle Mortgage Co.', font=boldf, fill=(0x1F, 0x3B, 0x63))
    d.text((70, 112), 'PO Box 1180 - Harbour City', font=small, fill=(0x55, 0x55, 0x5A))
    d.text((70, 190), title, font=boldf, fill=(0x22, 0x22, 0x26))
    y = 270
    hit = None
    for k, (a, b) in enumerate(rows):
        d.text((70, y), a, font=body, fill=(0x22, 0x22, 0x26))
        d.text((w - 70, y), b, font=body, fill=(0x22, 0x22, 0x26), anchor='ra')
        if k == circle_row:
            hit = (40, y - 14, w - 40, y + 52)
        y += 64
    return y, hit


def mortgage(path):
    w, h = 1024, 1325
    im = Image.new('RGB', (w, h), (0xF4, 0xF1, 0xEA))
    d = ImageDraw.Draw(im)
    boldf, body, small = font('Inter-Bold.otf', 44), font('Inter-Regular.otf', 30), font('Inter-Regular.otf', 24)
    rows = [('Statement date', 'Oct 1'), ('Account', '0042-7781'), ('Monthly payment', '$2,140.00'),
            ('Past due amount', '$4,280.00'), ('Late fees', '$190.00'), ('Total due by Oct 15', '$6,610.00')]
    y, hit = letter_page(d, w, 'MORTGAGE STATEMENT', rows, small, body, boldf, circle_row=3)
    for k, line in enumerate(['Your account is past due. If you are having', 'trouble making payments, call us to talk',
                              'about your options. Partial payments are', 'held until a full payment is received.']):
        d.text((70, y + 40 + k * 40), line, font=small, fill=(0x44, 0x44, 0x48))
    if hit:
        for k in range(3):                                      # the red pen circle, drawn twice over, a little wobbly
            d.ellipse([hit[0] - k * 3, hit[1] + k * 2, hit[2] + k * 2, hit[3] - k], outline=(0xC2, 0x2E, 0x26), width=5)
    d.text((620, y + 230), 'how??', font=font('Caveat-Bold.ttf', 56), fill=(0xC2, 0x2E, 0x26))
    im.save(path)


def final_notice(path):
    W, H = 1280, 1024
    im = Image.new('RGB', (W, H), (0xF4, 0xF1, 0xEA))
    d = ImageDraw.Draw(im)
    pw = int(W * 0.62)
    page = Image.new('RGB', (pw, H), (0xF4, 0xF1, 0xEA))
    pd = ImageDraw.Draw(page)
    boldf, body, small = font('Inter-Bold.otf', 40), font('Inter-Regular.otf', 26), font('Inter-Regular.otf', 21)
    pd.text((50, 50), 'Pennywhistle Mortgage Co.', font=boldf, fill=(0x1F, 0x3B, 0x63))
    pd.text((50, 150), 'FINAL NOTICE', font=font('Inter-Bold.otf', 72), fill=(0xB0, 0x22, 0x1C))
    for k, line in enumerate(['Your account is 60 days past due.', 'Pay $6,610.00 by October 31', 'to keep your loan in good standing.',
                              '', 'If we do not hear from you, we may', 'begin the foreclosure process.']):
        pd.text((50, 290 + k * 46), line, font=body, fill=(0x22, 0x22, 0x26))
    pd.text((50, H - 120), 'Account 0042-7781', font=small, fill=(0x55, 0x55, 0x5A))
    im.paste(page, (0, 0))
    d.line([(pw, 0), (pw, H)], fill=(0xD8, 0xD2, 0xC4), width=6)
    # the envelope, standing on its end: its face turned a quarter (the address reads along the long side)
    ew = W - pw
    env = Image.new('RGB', (H, ew), (0xEC, 0xE4, 0xD2))
    ed = ImageDraw.Draw(env)
    ed.rectangle([520, 150, 940, 340], outline=(0xBD, 0xB4, 0x9E), width=4)       # the window
    ed.text((540, 200), 'ACE', font=body, fill=(0x30, 0x30, 0x34))
    ed.text((540, 250), "Ace's Cafe", font=body, fill=(0x30, 0x30, 0x34))
    stamp = Image.new('RGBA', (560, 170), (0, 0, 0, 0))
    sd = ImageDraw.Draw(stamp)
    sd.rectangle([6, 6, 553, 163], outline=(0xC2, 0x2E, 0x26, 230), width=10)
    sd.text((280, 88), 'FINAL NOTICE', font=font('Inter-Bold.otf', 72), fill=(0xC2, 0x2E, 0x26, 230), anchor='mm')
    stamp = stamp.rotate(8, expand=True)
    env.paste(stamp, (60, 40), stamp)
    env = env.rotate(90, expand=True)
    im.paste(env, (pw, 0))
    im.save(path)


def main(out):
    os.makedirs(out, exist_ok=True)
    for name, fn in (('LI_Tags.png', tags), ('LI_Whiteboard.png', whiteboard), ('LI_Mortgage.png', mortgage),
                     ('LI_FinalNotice.png', final_notice)):
        fn(os.path.join(out, name))
        print('texture', os.path.join(out, name))


if __name__ == '__main__':
    main(os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT))
