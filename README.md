
# Citacop  
**Aka. Convert Image to Amiga Color Palette (and then Save as IFF ILBM)**  
_By Arda 'ref' Erdikmen_

A simple tool to do the actual work.  
Citacop is written for OCS Systems. Deluxe Paint, Personal Paint, Amos, and other OCS/ESC image viewers will love your exported image files. :D  
Citacop exports highly compatible, uncompressed raw ILBM.

Supports Amiga Low-Res, Med-Res and Laced saving. Aspect ratio compansation when previewing Med-res images. 
2 to 32 Colors are supported. EHB and HAM modes are NOT supported.

## Features  
- 🖼️ **Color Quantization**: Reduce the color depth of images to a specified number of colors.  
- 🎨 **Amigafy an Image**: Generate Amiga palettes and apply them to your image to constrain colors to 4096 Amiga colors (this is not Amiga-compatible, of course—it’s for an authentic Amiga 500 look).  
- ✨ **Advanced Dithering**: Apply ordered dithering (Bayer matrix), Floyd-Steinberg dithering, or no dithering (dithering is kind of crap, but it’s what I want actually 😄).  
- 📏 **Flexible Image Resizing and Cropping**: Resize or crop images dynamically with aspect ratio locking.  
- 🔄 **Undo/Redo States**: Save and revert to previous states for non-destructive editing.  
- 🖌️ **Palette Visualization**: Preview reduced palettes in a dedicated floating window.  
- 💾 **Export Options**: Save images in multiple formats, including BMP, JPG, PNG, and Amiga IFF (ILBM).

## V1.1: Added functions:
- Load/Save palette files of Deluxe Paint 4 (SET), Personal Paint (PAL, COL), ILBM CMAP chunks (IFF), Adobe Color Table (ACT)
- Can edit palette
- Lock a palette and apply to any image
- 64color EHB is now supported. (Still no HAM though)
- New Dithering Modes

## Supported Platforms  
Windows (built with .NET Framework/Windows Forms)

## How to Use  
1. Load an image into the application.  
2. Select the desired color count, dithering method, and other settings.  
3. Select **Operations > Convert to Amiga Mode**.  
4. Save the processed image or export it as an Amiga-compatible IFF file.  

## Ideal For  
- Retro computer enthusiasts who want to work with Amiga palettes.  
- Artists and designers looking to create retro-styled pixel art.  
- Developers and hobbyists experimenting with color quantization and dithering techniques.  

## Installation  
1. Download the latest release from the **Releases** section.  
2. Extract the archive and run `Citacop.exe`.  

## Contributing  
Contributions are welcome! Feel free to submit issues, feature requests, or pull requests to improve Citacop.  

## Screenshot  
![Citacop Screenshot 2](https://github.com/user-attachments/assets/38b5a6c2-cc0b-491d-bf61-49217b1484cc)

