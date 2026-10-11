# Shared viewer icon

`space.png` is the transparent source artwork for both desktop viewers. `space.ico`
contains 32-bit PNG frames at 16, 24, 32, 48, 64, 128 and 256 pixels. Both projects
embed the ICO as their Windows executable icon and window resource. Avalonia also
embeds the PNG for its toolbar. Assets are linked from this directory so the two
applications use the same artwork without runtime sidecar files.

Created on 2026-10-11 with the built-in image generation tool. The ICO is a
high-quality bicubic size/format conversion of the generated transparent PNG.

Generation prompt:

> Use case: logo-brand. Asset type: square desktop application icon for an astronomy image viewer. Create one polished, playful comic-style space emblem: a bold turquoise and violet ringed fantasy planet with a warm golden four-point star, thick clean dark navy outlines, restrained cel-shading. Centered compact silhouette, easily recognizable at 16–48 pixels, generous clear outer margin, simple large shapes and very few details. Actual transparent background around the planet/star silhouette, no tile or backdrop. Charming and stylish, no realism needed. No letters, no words, no numerals, no library name, no watermark, no icon sheet, no mockup. Deliver a single square raster image with alpha transparency.
