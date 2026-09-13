"""Compose unretouched Blender inspection renders. No generative imagery."""
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat, __version__

ROOT = Path(__file__).resolve().parents[2]
PREVIEW = ROOT / 'ArtSource/Blender/Ships/FusionFrigate/Previews'
EVIDENCE = ROOT / 'docs/verification/FusionFrigate'
FONT = Path('C:/Windows/Fonts/msyh.ttc')


def font(size):
    return ImageFont.truetype(str(FONT), size) if FONT.exists() else ImageFont.load_default()


def compose(path, title, subtitle, entries, columns, tile=(960, 640)):
    gap, band, header, footer = 20, 42, 100, 45
    rows = (len(entries) + columns - 1) // columns
    width = gap + columns * (tile[0] + gap)
    height = header + rows * (tile[1] + band + gap) + footer
    sheet = Image.new('RGB', (width, height), '#20272f')
    draw = ImageDraw.Draw(sheet)
    draw.text((gap, 15), title, font=font(32), fill='#f0f3f4')
    draw.text((gap, 60), subtitle, font=font(20), fill='#b8c8d5')
    for i, (label, image_path) in enumerate(entries):
        x = gap + (i % columns) * (tile[0] + gap)
        y = header + (i // columns) * (tile[1] + band + gap)
        draw.text((x + 8, y + 6), label, font=font(22), fill='#d8e2e8')
        with Image.open(image_path) as image:
            image = image.convert('RGB')
            if image.size != tile:
                image = image.resize(tile, Image.Resampling.LANCZOS)
            sheet.paste(image, (x, y + band))
    draw.text((gap, height - 35), '真实 Blender 模型检视 · 无发光 / 尾焰 / Bloom · 单次仅显示一个 LOD',
              font=font(19), fill='#b8c8d5')
    sheet.save(path)


compose(PREVIEW / 'FusionFrigate_Inspection.png', 'FusionFrigate | 模型检视',
        'LOD0 17,224 三角形  ·  长 22.14 m  ·  4 共享材质 / 0 贴图  ·  中性 Workbench 预览',
        [(label, PREVIEW / f'FusionFrigate_LOD0_{view}.png') for label, view in [
            ('01  三分之四视角', 'ThreeQuarter'), ('02  侧视', 'Side'),
            ('03  顶视', 'Top'), ('04  尾视：1 主引擎 + 4 辅助引擎', 'Rear')]], 2)
compose(PREVIEW / 'FusionFrigate_LOD_Comparison.png', 'FusionFrigate | LOD 对照',
        '每档相同原点、轴向及整体尺寸；图中分别单独渲染后排版。',
        [(f'LOD{lod} · {count:,} 三角形 · {label}', PREVIEW / f'FusionFrigate_LOD{lod}_{view}.png')
         for label, view in [('三分之四', 'ThreeQuarter'), ('尾视', 'Rear')]
         for lod, count in [(0, 17224), (1, 5400), (2, 1396)]], 3, (720, 480))
comparison = {'pillow_version': __version__, 'method': 'Mean absolute RGB difference between same-camera Workbench renders; 0..255 channel scale.', 'views': {}}
for view in ('ThreeQuarter', 'Rear', 'DriveDetail'):
    source = Image.open(PREVIEW / f'FusionFrigate_LOD0_{view}.png').convert('RGB')
    reimport = Image.open(EVIDENCE / f'FBX_LOD0_{view}.png').convert('RGB')
    difference = ImageChops.difference(source, reimport)
    comparison['views'][view] = {'mean_absolute_rgb_error': ImageStat.Stat(difference).mean}
(EVIDENCE / 'render_comparison.json').write_text(json.dumps(comparison, indent=2), encoding='utf-8')
print(json.dumps(comparison))
print('INSPECTION_SHEETS_CREATED')
