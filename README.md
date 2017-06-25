# Kiseki Texture Tool

This is a WIP, work is not finished, some code is spaghetti (at least by my standards, not to mention the constant firing of the Garbage Collector due to yet to be done memory optimizations), but some may find it useful right now, the functionality is complete and the tool functions as expected.

._CP documentation, being the format I reverse engineered from scratch including the ._CH variant for spritesheets is coming soon but if you have basic understanding of C# or a C based language, you should be able to probably figure it out in the meantime as the application is well commented and should be relatively easy to follow if you are in posession of adequate knowledge. 

# Functions/Functionalities
* Extraction of Sora no Kiseki image assets (FC, SC, 3rd), `(maybe even Zero/Ao's, don't have those games).`
* Re-importing or conversion of assets back into the game's original format, allowing for texture modifications.
* Full Alpha support, with per pixel transparency (AFAIK Falcnvrt sometimes loses sprite sheet transparency).
* Full support for sprite sheet reading and writing.
* Automatic conversion into PNG format if specified by user, or DDS => PNG.
* Exporting sprite sheets as individual frames (CH Tool Style) or like RAW Data/Falcnvrt style (as one image).
* Manual overrides for extraction options (helps in the very rare cases you get corrupt colours etc.)
* A few debugging options if you want to mess about.

# Supported Files | Extraction + Conversion into. 

| Extension       | File Type       | Notes  |
|:-------------:|:-------------:|:-----:|
| ._DS | Hidden DDS file, under a different extension.    | N/A |
| ._CH | RAW list of pixels, stored in u4444, u1555 or u8888 bits per pixel.    | Files have no header, generic filters are implemented to attempt to guess the bits per pixel format, effectiveness ~95% correct guesses based on file metadata. Bits per pixel and varioud options can be manually specified in case of extraction with broken colours etc. |
| ._CH + ._CP | CH: RAW list of pixels, stored in u4444. CP: Instructions to compose chip/complete sprite sheet.     | CH Stores small image chunks of 16x16, no header again, chunks may be reused multiple times. CP file references these chunks as well as null chunks to re-construct multiple frames of the sprite sheet. Resolution: 256x256 for each frame, following frames add 256 to height and appear below. |

Note: My logic of re-construction of the sprite files or `chips`, composed of ._CP and ._CH files does not follow the same logic as Falcom's own tool/compiler/script etc. The order in which the ._CP and ._CH chunks are generated, the ._CP file is constructed manually and the individual image chunks calculated and repacked is different than Falcom's own but still 100% compatible and entirely lossless. This means that the reimporter for the sprite sheets will not produce binary comparable files to the original.

# Arguments

This is a console/command line/shell utility, thus must be approprtiately launched from cmd, bash (should probably function if you have Wine installed on Linux) or any other kind of shell or terminal.

Order of arguments does not matter, with the exceptions of the ones which take parameters e.g. `--file`

### Core

| Argument       | Function       | Notes  |
|:-------------:|:-------------:|:-----:|
| No argument | Displays a help page with some info and usage tips. More guidance there.    | N/A |
| --file `|` -f <File/Directory Path> | Specifies a file or folder to be converted or recompressed. | Must be supplied |
| -e `|` --extract | Extracts the directory or specified file, by conversion to .dds | N/A |
| -c `|` --compress | Compresses the directory or specified files. | N/A  |
| -u `|` --colorprofile | Specifies the bits per byte of the file or group of files chosen, see the help page in the application. | Must be specified when reimporting. Optional override (if necessary) when exporting. Original colour profile is attached to file name when extracting.  |
| --spritesheetcompress | Use this in place of --compress when you want to re-create/re-import sprite sheets. | You must specify a directory, not a file.  |

### Extra

| Argument       | Function       | Notes  |
|:-------------:|:-------------:|:-----:|
| --convert | Automatically converts output .DDS files to .PNG | N/A  |
| --nosplit | Exports the sprite sheet (if a sprite sheet), into a singluar file with all frames instead of individual frames. | Extracts exactly as represented in the ._CP + ._CH file, but can hit an integer overflow on opening DDS for sprite sheets with huge numbers of frames 30+ when trying to load the .DDS file by external program or as part of conversion to .PNG, not recommended for large sprite sheets.  |
| --nodelete | Do not delete the original input files or intermediate files when converting. | Tool normally removes intermediate files e.g. DDS when converting from PNG to ._CH, as well as the original.  |
| --dumpchunks | Debugging use, or for fellow devs wanting to improve/replace this tool. | Ignores the ._CP file for sprite sheet. Exports raw 16x16 chunks into one file.  |

#### Console Usage Examples 
```
Usage ( => DDS): KisekiCHConverter.exe --extract -f <CHFile>
Usage ( => DDS => PNG): KisekiCHConverter.exe --extract -f <CHFile> --convert
Usage ( DDS/PNG => CH): KisekiCHConverter.exe --compress -f <CHFile> -u 2
Usage ( => CH/CP, Single File): KisekiCHConverter.exe --spritesheetcompress -f <CHFile> -u 2
Usage ( => CH/CP, Multiple Frames): KisekiCHConverter.exe --spritesheetcompress -f <CHDirectory> -u 2
```

# Media ! 
I originally wrote this in order to allow modification of the sprites, textures and image assets in Kiseki titles, but well... there are much more possibilities with that... take this example:

![Princess Joshua!](http://i.imgur.com/0DQWdlR.gif "The cutest thing alive falls gracefully.") ![Estelle Victory](http://i.imgur.com/vCt3Mh2.gif "Estelle Victory Pose!") ![Tita S-Craft](http://i.imgur.com/FZjwGOS.gif "Tita S-Craft!")
![Tita Gives Up](http://i.imgur.com/eVa44FZ.gif "Don't give up, Tita!") ![Renne Slap](http://i.imgur.com/vJjaLFe.gif "Estelle's attempt to knock some sense into this poor girl") ![Farewell, Phantasma!](http://i.imgur.com/09HYr3O.gif "Farewell, Phantasma!")

Modification of in-game sprite assets.

# Did someone say Wine?

This application makes use of the old Legacy Nvidia Texture Tools library, licensed under the MIT license in executable format to act as the method of conversion between various DirectX formats and back while this application is GPL V3 licensed ([tl;dr Legal aspects for the interested,](https://tldrlegal.com/license/gnu-general-public-license-v3-(gpl-3))).
I might replace this in the future and use another DirectX Texture supporting library, but this keeps me happy for now.

As for functions involving things such as arguments and path building, I cannot guarantee that this software will work on Linux for the time being, since I've not tested it myself yet and I might need to patch this tool with alternate arguments for nvdxt if so. If it doesn't yet work, getting it to work on Linux will probably be hassle free, although I bet this should work on Wine already, I was thinking of native builds running and compiled via Mono.

PS. I'm a Linux user myself.