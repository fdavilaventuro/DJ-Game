import os
import base64
import shutil
from io import BytesIO
from pathlib import Path
from PIL import Image
from mutagen.oggvorbis import OggVorbis
from mutagen.flac import Picture

script_dir = Path(__file__).parent
input_dir = script_dir
output_dir = script_dir.parent / "Assets" / "StreamingAssets" / "Music"
output_dir.mkdir(parents=True, exist_ok=True)

def make_square(img):
    w, h = img.size
    if w == h:
        return img
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    return img.crop((left, top, left + side, top + side))

def process_file(src_path):
    dst_path = output_dir / src_path.name
    shutil.copy2(src_path, dst_path)
    audio = OggVorbis(dst_path)
    if "metadata_block_picture" not in audio:
        return
    pic_b64 = audio["metadata_block_picture"][0]
    raw = base64.b64decode(pic_b64)
    # Parse the FLAC Picture block
    pic = Picture(raw)
    # Extract the image data
    img = Image.open(BytesIO(pic.data)).convert("RGB")
    img_sq = make_square(img)
    buf = BytesIO()
    img_sq.save(buf, format="JPEG", quality=97)
    jpg_bytes = buf.getvalue()
    pic = Picture()
    pic.type = 3
    pic.mime = "image/jpeg"
    pic.desc = ""
    pic.width, pic.height = img_sq.size
    pic.depth = 24
    pic.data = jpg_bytes
    encoded = base64.b64encode(pic.write()).decode("ascii")
    audio["metadata_block_picture"] = [encoded]
    audio.save()

def main():
    for f in input_dir.iterdir():
        if f.is_file() and f.suffix.lower() == ".ogg":
            process_file(f)
    # Delete original OGG files after processing
    for f in input_dir.iterdir():
        if f.is_file() and f.suffix.lower() == ".ogg":
            f.unlink()
    print("Processing complete. Files in:", output_dir)

if __name__ == "__main__":
    main()