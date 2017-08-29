#define SAFE_MODE

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace KisekiCHConverter
{
    class Program
    {
        static string FilePath;
        static string FileName;
        static string FileDirectory;
        static string HeaderName; // Append to specify header name

        static byte Action = 0;
        static byte ColorProfile = 0;
        static bool ConvertToPNGFlag = false;
        static bool DeleteFlag = true;
        static bool DumpChunks = false; // Whether to dump chunks instead of the sprites.
        static bool NoSplit = false;

        static uint ImageHeight = 0;
        static uint ImageWidth = 0;
        static int OriginalFileSize;

        static byte[] CHFile;
        static byte[] CHHeader;

        static byte[] CPFile; // Used for chips/sprite sheets.
        static byte MultiplierColourSpace; // Shall a u8888 sprite/chip/sheet appear, I WILL FIGHT IT WITH HONOUR! 
        static bool WasCPFile; // Used for verification whether the rest of a certain series of steps would be necessary for completion. Used once due lazy programming.
        static ushort ChipSpriteSheetAmountOfFrames; // Derived from ._CP File for Sprite Sheets, first ushort.
        static ushort ChipSpriteAmountOfChunks; // Derived from ._CH File for Sprite Sheets, first ushort.
        const ushort ChipWidth = 256; // Width of chips/sprite animations.
        static ushort ChunkLevel = 0; // Which level is the chunk on currently in terms of height? 1 = Top;
        const byte ChunkWidth = 16;

        static List<byte> CPFileList = new List<byte>(); // This one is used for building the CPFile.
        static List<byte> CHFileList = new List<byte>();

        static byte[] TwoByteStorage = new byte[2];
        static byte[] FourByteStorage = new byte[4];

        // The main entry point of the program.
        static void Main(string[] args)
        {
            // Check Arguments
            for (int x = 0; x < args.Length; x++)
            {
                if (args[x] == ("-f") | args[x] == ("--file")) { FilePath = args[x + 1]; }
                else if (args[x] == ("-e") | args[x] == ("--extract")) { Action = 1; }
                else if (args[x] == ("-c") | args[x] == ("--compress")) { Action = 2; }
                else if (args[x] == ("--convert")) { ConvertToPNGFlag = true; }
                else if (args[x] == ("--nodelete")) { DeleteFlag = false; }
                else if (args[x] == ("--dumpchunks")) { DumpChunks = true; }
                else if (args[x] == ("--nosplit")) { NoSplit = true; }
                else if (args[x] == ("--spritesheetcompress")) { Action = 3; }
                else if (args[x] == ("-u") | args[x] == ("--colorprofile")) { ColorProfile = Convert.ToByte(args[x + 1]); }
            }

            if (Action == 0) { DisplayHelp(); Environment.Exit(0); }
            else if (Action == 1)
            {
                if (Directory.Exists(FilePath))
                {
                    string[] FilesInDirectory = Directory.GetFiles(FilePath, "*._CH");
                    for (int x = 0; x < FilesInDirectory.Length; x++)
                    {
                        FilePath = FilesInDirectory[x];
                        DecompressFile();
                        ImageHeight = 0;
                        ImageWidth = 0;
                    }
                }
                else
                {
                    DecompressFile();
                }
            }
            else if (Action == 2)
            {
                if (Directory.Exists(FilePath))
                {
                    string[] FilesInDirectory = Directory.GetFiles(FilePath);
                    for (int x = 0; x < FilesInDirectory.Length; x++)
                    {
                        FilePath = FilesInDirectory[x];
                        CompressFile();
                    }
                }
                else
                {
                    CompressFile();
                }
                
                Environment.Exit(0);
            }
            else if (Action == 3) { CompressSpriteSheet(); Environment.Exit(0); }

            if (ConvertToPNGFlag && NoSplit)
            {
                LoadDDSSavePNG(FilePath.Substring(0, FilePath.Length - 3) + "dds", FilePath.Substring(0, FilePath.Length - 3) + "png");
            }
            else if (ConvertToPNGFlag && ! NoSplit)
            {
                string[] FilesInDirectory = Directory.GetFiles(FileDirectory, "*.dds");
                foreach (string DDSFile in FilesInDirectory)
                {
                    LoadDDSSavePNG(DDSFile, DDSFile.Substring(0, DDSFile.Length - 3) + "png");
                }
            }

        }

        private static void LoadDDSSavePNG(string FileToLoad, string FileToSave)
        {
            System.Drawing.Image DDSImage = Imaging.DDSReader.DDS.LoadImage(FileToLoad);
            DDSImage.Save(FileToSave, System.Drawing.Imaging.ImageFormat.Png);
            if (DeleteFlag) { File.Delete(FileToLoad); }
        }

        private static void DecompressFile()
        {
            // Copy file to Array
            CHFile = File.ReadAllBytes(FilePath);

            // Calculate the File Size
            OriginalFileSize = CHFile.Length;

            // Get directory of file.
            FileDirectory = new FileInfo(FilePath).Directory.FullName;
            FileDirectory.Trim();

            // Obtain the File Name of the File.
            FileName = FilePath;
            if (FilePath.Length > FileDirectory.Length) { FileName = FilePath.Substring(FileDirectory.Length + 1); }
            // +1 compensates for resolved path

            // Check if the file is a sprite sheet, and continue on a different path if it is :)
            HandleChipSpriteSheet();

            // If it was not a sprite sheet/chip, just convert as regular image.
            if (! WasCPFile)
            {
                // Set User Set Colour Profile if the user has specified one
                if (ColorProfile != 0) { SetColourProfile(); }
                else { RunDetectionFilters(); }// Else Automatically Determine Bits Per Pixel for each Pixel

                // Determine Width of Image if not Explicitly Set (In most filters, the height is fixed and not the width, so if the width is 0, it is calculated from the height).
                if (ImageWidth == 0) { ImageWidth = (uint)((OriginalFileSize / 2) / ImageHeight); }

                // Dump the File as DDS
                DumpFileAsDDS();
            }
        }

        private static void DumpFileAsDDS()
        {
            // Set the height and width into the header.
            byte[] ImageWidthArray = BitConverter.GetBytes(ImageWidth); byte[] ImageHeightArray = BitConverter.GetBytes(ImageHeight);

            // Insert Width and Height into Header
            CHHeader[0xC] = ImageHeightArray[0]; CHHeader[0xD] = ImageHeightArray[1]; CHHeader[0xE] = ImageHeightArray[2]; CHHeader[0xF] = ImageHeightArray[3];
            CHHeader[0x10] = ImageWidthArray[0]; CHHeader[0x11] = ImageWidthArray[1]; CHHeader[0x12] = ImageWidthArray[2]; CHHeader[0x13] = ImageWidthArray[3];

            // Store the new file to be written in a byte array and write the new file to disk.
            byte[] NewFile = CHHeader.Concat(CHFile).ToArray();
            File.WriteAllBytes(FilePath.Substring(0, FilePath.Length - 4) + HeaderName + ".dds", NewFile);

            // Delete the original file, or don't depending on user action.
            if (DeleteFlag) { File.Delete(FilePath); }

            // Used if automatically converting to PNG after conversion.
            FilePath = FilePath.Substring(0, FilePath.Length - 4) + HeaderName + ".dds";
        }

        private static void CompressFile()
        {
            // Convert the file to a DirectDrawSurface
            ConvertFileToDDS(FilePath);

            // Remove the original file (or not).
            if (DeleteFlag) { File.Delete(FilePath); }

            // Remove The File Header Using Arrays, Write All Bytes and Move the File to the new extension
            CHFile = File.ReadAllBytes(FilePath + ".dds");
            CHFile = CHFile.Skip(0x80).ToArray();
            File.WriteAllBytes(FilePath + ".dds", CHFile);
            File.Move(FilePath + ".dds", FilePath.Substring(0, FilePath.Length - 4) + "._CH");
        }

        private static void ConvertFileToDDS(string FilePath)
        {
            Process DXTProcess = new Process();
            // Launches embedded nvdxt, bits depends on colour profile
            switch (ColorProfile)
            {
                case 1:
                    DXTProcess.StartInfo.FileName = System.AppDomain.CurrentDomain.BaseDirectory + "nvdxt.exe";
                    DXTProcess.StartInfo.Arguments = "-file " + "\"" + FilePath + "\"" + " -u1555 -nomipmap -output " + "\"" + FilePath + ".dds" + "\"";
                    DXTProcess.StartInfo.UseShellExecute = false;
                    DXTProcess.StartInfo.RedirectStandardOutput = true;
                    DXTProcess.StartInfo.RedirectStandardError = true;
                    DXTProcess.Start();
                    DXTProcess.WaitForExit();
                    break;
                case 2:
                    DXTProcess.StartInfo.FileName = System.AppDomain.CurrentDomain.BaseDirectory + "nvdxt.exe";
                    DXTProcess.StartInfo.Arguments = "-file " + "\"" + FilePath + "\"" + " -u4444 -nomipmap -output " + "\"" + FilePath + ".dds" + "\"";
                    DXTProcess.StartInfo.UseShellExecute = false;
                    DXTProcess.StartInfo.RedirectStandardOutput = true;
                    DXTProcess.StartInfo.RedirectStandardError = true;
                    DXTProcess.Start();
                    DXTProcess.WaitForExit();
                    break;
                case 3:
                    DXTProcess.StartInfo.FileName = System.AppDomain.CurrentDomain.BaseDirectory + "nvdxt.exe";
                    DXTProcess.StartInfo.Arguments = "-file " + "\"" + FilePath + "\"" + " -u8888 -nomipmap -output " + "\"" + FilePath + ".dds" + "\"";
                    DXTProcess.StartInfo.UseShellExecute = false;
                    DXTProcess.StartInfo.RedirectStandardOutput = true;
                    DXTProcess.StartInfo.RedirectStandardError = true;
                    DXTProcess.Start();
                    DXTProcess.WaitForExit();
                    break;
            }
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("\n-----------------------");
            Console.WriteLine("CHConvert by Sewer56lol");
            Console.WriteLine("-----------------------");
            Console.WriteLine("This tool was quickly written to fill a small gap in the lack of a dedicated\n" +
                              "conversion utility to convert ._CH files, functionality similar akin to falcnvrt.");
            Console.WriteLine("-----------------------");
            Console.WriteLine("Usage ( => DDS): KisekiCHConverter.exe --extract -f <CHFile>");
            Console.WriteLine("Usage ( => CH): KisekiCHConverter.exe --compress -f <CHFile> -u 2");
            Console.WriteLine("Usage ( => CH/CP, Single File): KisekiCHConverter.exe --spritesheetcompress -f <CHFile> -u 2");
            Console.WriteLine("Usage ( => CH/CP, Multiple Frames): KisekiCHConverter.exe --spritesheetcompress -f <CHDirectory> -u 2\n");
            Console.WriteLine("Supported Formats: \n| Tested | PNG, DDS \n| Untested | TGA, BMP, GIF, PPM, JPG, TIFF (.tif), .CEL, PSD, .rgb, *.bw, .rgba\n");
            Console.WriteLine("Arguments:");
            Console.WriteLine("     `--file` <file> | `-f` <file> : Specifies a file.");
            Console.WriteLine("     `--extract`                   : Tells the tool to extract.");
            Console.WriteLine("     `--compress`                  : Tells the tool to compress.");
            Console.WriteLine("     `--spritesheetcompress`       : Compresses the image to act akin to a spritesheet.");
            Console.WriteLine("     `--colorprofile` <profile> | `-u` <profile> : Specify a colour profile.\n");
            Console.WriteLine("     `--convert`                   : Automatically converts the output images to PNGs.");
            Console.WriteLine("     `--nodelete`                  : Do not delete the source image (for --convert & --extract).");
            Console.WriteLine("     `--nosplit`                   : Do not split spritesheets into separate images.");
            Console.WriteLine("     `--dumpchunks` (Debugging use): If the original image is a spritesheet, " +
                              "\n                                   dump individual chunks instead of arranging the sprites.\n");
            Console.WriteLine("A Colour Profile is optional for --extract, necessary for --compress.");

            Console.WriteLine("\n--------------- | u1555: -u 1");
            Console.WriteLine("Colour Profiles | u4444: -u 2");
            Console.WriteLine("--------------- | u8888: -u 3");

            Console.WriteLine("\n---------");
            Console.WriteLine("Usage Tips");
            Console.WriteLine("---------- ");

            Console.WriteLine("\n1. Always recompress with the same Colour Profile in exported file name." +
                              "\n2. If you don't know it, first try u4444 with `-u 2`." +
                              "\n3. There is no sanitization or checks, so don't screw up your commands." +
                              "\n4. Sprite Sheets: Use empty directories, only relevant PNGs/DDS(es) or other supported formats." +
                              "\n5. Reminder to not forget to specify the colour profile." +
                              "\n6. Do not strip header names or frames from sprite sheet files/frames (I was lazy)." +
                              "\n\nUse at your own risk.");
            Console.ReadLine();
        }



        // If the file is a Chip/Sprite Sheet, then give it some special treatment.
        private static void HandleChipSpriteSheet()
        {
            if (FileName.StartsWith("CH"))
            {
                // Width is always ChunkWidth, fixed, image is rearranged from tiles.
                ImageWidth = 16;

                // Get the amount of chunks from the image.
                TwoByteStorage[0] = CHFile[0]; TwoByteStorage[1] = CHFile[1];
                ChipSpriteAmountOfChunks = (ushort)BitConverter.ToUInt16(TwoByteStorage, 0);

                // Remove Chunk Length Information From Image :)
                CHFile = CHFile.Skip(2).ToArray();

                // Get Image Height
                ImageHeight = ChipSpriteAmountOfChunks * ImageWidth;

                // Description inside
                // RealignCharacterSprites();
                // Above method now unused.

                // Image resolution will be reset here again

                if (DumpChunks != true)
                {
                    LoadCPFile();
                    RearrangeChunksCPFile();
                }

                if (NoSplit)
                {
                    // Set the header of the image :)
                    CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444";

                    // Save the completed File!
                    DumpFileAsDDS();
                    WasCPFile = true;
                }
                else
                {
                    // Set the header of the image :)
                    CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444";
                    DumpFramesAsDDS();
                    WasCPFile = true;
                }

            }
        }

        private static void DumpFramesAsDDS()
        {
            // Set image height to be height of frame
            ImageHeight = 256; ImageWidth = 256;

            // Set the height and width into the header.
            byte[] ImageWidthArray = BitConverter.GetBytes(ImageWidth); byte[] ImageHeightArray = BitConverter.GetBytes(ImageHeight);

            // Insert Width and Height into header to paste.
            CHHeader[0xC] = ImageHeightArray[0]; CHHeader[0xD] = ImageHeightArray[1]; CHHeader[0xE] = ImageHeightArray[2]; CHHeader[0xF] = ImageHeightArray[3];
            CHHeader[0x10] = ImageWidthArray[0]; CHHeader[0x11] = ImageWidthArray[1]; CHHeader[0x12] = ImageWidthArray[2]; CHHeader[0x13] = ImageWidthArray[3];

            // Each frame here will exist as a drawable.
            List<byte[]> IndividualFrames = new List<byte[]>();

            // Read every frame into separate file/byte array.
            for (int x = 0; x < ChipSpriteSheetAmountOfFrames; x++)
            {
                byte[] IndividualFrame = new byte[ImageWidth*ImageHeight*MultiplierColourSpace]; // Allocate sufficient memory for each pixel.
                int StartingByteSource = (int) (ImageWidth * ImageHeight * MultiplierColourSpace * x);
                CopyBytesToArray(IndividualFrame.Length, StartingByteSource, 0, CHFile, IndividualFrame);
                IndividualFrames.Add(IndividualFrame);
            }

            // Get directory of file.
            FileDirectory = new FileInfo(FilePath).Directory.FullName;
            FileDirectory.Trim();
            FileDirectory += @"\"; // Append "\"
            // Obtain the File Name of the File.
            FileName = FilePath; if (FilePath.Length > FileDirectory.Length) { FileName = FilePath.Substring(FileDirectory.Length); }

            // +1 compensates for resolved path

            // Create directory for individual frames.
            Directory.CreateDirectory(FileDirectory + FileName.Substring(0, FileName.Length - 4).Trim());

            // Append headers and write out files.
            for (int x = 0; x < IndividualFrames.Count; x++)
            {
                byte[] NewFile = CHHeader.Concat( IndividualFrames[x] ).ToArray();
                File.WriteAllBytes(FileDirectory + FileName.Substring(0, FileName.Length - 4).Trim() + @"\" + FileName.Substring(0, FileName.Length - 4) + "_" + x.ToString("00000") + ".dds" , NewFile);
            }
        }

        // This is no longer necessary, a programming mistake or silly oversight has led to this ;/ Sewer wasted 4 hours on this if not more, Sewer pls.
        private static void RealignCharacterSprites()
        {
            // This one is pretty interesting and should probably require an entire documentation of its own...
            // Basically the chips/sprite sheets are small 16x16 pixel chunks which are rearranged by the ._CP file, however the chunks aren't perfect in nature...

            // Simply put, the first column of the output is actually the last column and all columns of pixels need to be shifted and the first column put as last column, offset by 1 up.
            // The CP File is then read and rearranged the fixed chunks.

            // Required Elements:
            // CHFile: File Data of Sprite
            // ImageHeight: To Determine Pixels to Move
            // ImageWidth: Pixels per row!
            // ChipSpriteAmountOfChunks: Amount of Chunks in ._CH file, it is required that this is read and stripped from the file.

            // Calculate Bytes per pixel of image format.
            if (CHHeader == Properties.Resources.Header_u8888) { MultiplierColourSpace = 3; } // 32 Bits per Pixel is u8888
            else { MultiplierColourSpace = 2; } // Else 24 bits per pixel

            // Make Byte Array of Size Capable of Holding the first column of pixels.
            byte[] FirstImageRow = new byte[ImageHeight * MultiplierColourSpace];
            int CurrentPixel; // Current Pixel being Manipulated
            
            // For each column
            for (int x = 0; x < ImageHeight; x++)
            {
                // Get first pixel (byte location) in current row.
                CurrentPixel = (int)ImageWidth * x * MultiplierColourSpace;

                // Place First Pixel in Grid
                CopyBytesToArray(MultiplierColourSpace, CurrentPixel, (x * MultiplierColourSpace), CHFile, FirstImageRow); // Copy individual pixel from Array A to Array B.
            }

            // After copy operation, every MultiplierColourSpace'th entry into FirstImageRow Array * Width will be the first pixel for MultiplierColourSpace pixels.

            // Create a new array for storing the file.
            Byte[] CHFileNew = new Byte[CHFile.Length];

            int y = MultiplierColourSpace; // Will be used for determining where to copy from. It is the starting byte for the copy operation.

            // Now we will just rewrite the new file as the old file with the new pixels in!
            for (int x = 0; x <= CHFile.Length; x++)
            {
                // This loop is first indexed for the modulo operation!

                // For every 16th pixel except first!
                if (x % (ChunkWidth*MultiplierColourSpace) == 0 && x != 0)
                {
                    // Copy the pixel to the end of the row!
                    if (y == FirstImageRow.Length)
                    {
                        y = 0; CopyBytesToArray(MultiplierColourSpace, y, x - 2, FirstImageRow, CHFile); // This will fire on last loop to copy the originally topmost pixel.
                    } 
                    else { CopyBytesToArray(MultiplierColourSpace, y, x, FirstImageRow, CHFile); } // This will always fire except on the last loop.
                    y += MultiplierColourSpace;
                }
                else
                {
                    // Otherwise just copy the remaining pixels, which will now come earlier in the file by the offset of 0xMultiplierColourSpace
                    try { CHFileNew[x] = CHFile[x + MultiplierColourSpace]; }
                    catch { }
                }
            }

        }

        private static void RearrangeChunksCPFile()
        {
            // Notes:
            // ushort in Header: Number of Frames
            // FF FF = Insert Blank 16x16 chunk.
            // Otherwise ushort with chunk ID to insert.

            // Required Elements:
            // CHFile: File Data of Sprite
            // ImageHeight: To Determine Pixels to Move
            // ImageWidth: Pixels per row!
            // ChipSpriteAmountOfChunks: Amount of Chunks in ._CH file, it is required that this is read and stripped from the file.

            // Calculate Bytes per pixel of image format.
            if (CHHeader == Properties.Resources.Header_u8888) { MultiplierColourSpace = 3; } // 32 Bits per Pixel is u8888
            else { MultiplierColourSpace = 2; } // Else 24 bits per pixel

            // Work out the resolution of the image.
            ImageWidth = 256; // Width = 256, fixed
            ImageHeight = ChipSpriteSheetAmountOfFrames * ImageWidth; // Image Height = Width * Frames, Each Frame is 256x256

            List<Byte[]> ChunkList = new List<byte[]>();

            // TODO:
            // WARNING, IF BY ANY MEANS IN THE FUTURE, THE SPRITE RESOLUTIONS WILL CHANGE, THIS WILL ALSO CHANGE.
            for (int x = 0; x < ChipSpriteAmountOfChunks; x++)
            {
                byte[] Chunk = new Byte[ChunkWidth * ChunkWidth * MultiplierColourSpace]; // 16x16 Chunks
                for (int ByteNo = 0; ByteNo < Chunk.Length; ByteNo++) // For each byte, copy byte to chunk!
                {
                    Chunk[ByteNo] = CHFile[ (x*256*MultiplierColourSpace) + ByteNo]; // Copy Pixels to Chunk!
                } 
                ChunkList.Add(Chunk); // Add the chunk yay!
            }

            // Create a file of appropriate size to house new image.
            Byte[] NewDDSFile = new Byte[ImageWidth * ImageHeight * MultiplierColourSpace];

            ushort ChunkNumber;
            uint ChunkPosition = 0; // Physical Byte where the Chunk is currently located.

            // Time to read the file and insert chunks as necessary.
            for (int x = 2; x < CPFile.Length;) // x = 2, skip the header!
            {
                TwoByteStorage[0] = CPFile[x]; TwoByteStorage[1] = CPFile[x + 1]; ChunkNumber = BitConverter.ToUInt16(TwoByteStorage, 0); // Get Chunk Number

                if (ChunkNumber == 0xFFFF) { ChunkPosition = IncrementChunkPosition(ChunkPosition, ImageWidth, MultiplierColourSpace); } // If no chunk, do not draw!
                else // Otherwise draw a chunk
                {
                    // Lets try overflow.
                    if ( ! (ChunkNumber >= ChunkList.Count) )
                    {
                        DrawChunk(ChunkPosition, ImageWidth, ChunkList[ChunkNumber], NewDDSFile, MultiplierColourSpace);
                    }
                    else
                    {
                        Console.WriteLine("Overflow! Chunk Offset: " + x);
                        Console.WriteLine("Overflow! Chunk Bytes: " + CPFile[x] + " " + CPFile[x + 1]);
                        Console.WriteLine("Overflow! Chunk Number: " + ChunkNumber + "\n");
                        Console.ReadLine();
                    }
                    ChunkPosition = IncrementChunkPosition(ChunkPosition, ImageWidth, MultiplierColourSpace);
                }

                x += 2; // Move to next chunk Entry!
            }

            CHFile = NewDDSFile;
        }

        private static void CompressSpriteSheet()
        {
            bool IsDirectory = false;

            // Calculate Bytes per pixel of image format.
            if (CHHeader == Properties.Resources.Header_u8888) { MultiplierColourSpace = 3; } // 32 Bits per Pixel is u8888
            else { MultiplierColourSpace = 2; } // Else 24 bits per pixel


            if (Directory.Exists(FilePath)) // If supplied directory, merge all the files.
            {
                IsDirectory = true;
                ImageHeight = 256; ImageWidth = 256;
                // Convert all files to dds
                string[] FilesInDirectory = Directory.GetFiles(FilePath);

                for (int x = 0; x < FilesInDirectory.Length; x++)
                {
                    ConvertFileToDDS(FilesInDirectory[x]);
                    if (DeleteFlag) { File.Delete(FilesInDirectory[x]); }
                    FilesInDirectory[x] = FilesInDirectory[x] + ".dds";
                }

                List<byte[]> IndividualFrameFiles = new List<byte[]>();
                // Load all files into bytes, strip them of their headers and move to a list.
                for (int x = 0; x < FilesInDirectory.Length; x++)
                {
                    byte[] CHFrameFile = File.ReadAllBytes(FilesInDirectory[x]);
                    CHFrameFile = CHFrameFile.Skip(0x80).ToArray();
                    IndividualFrameFiles.Add(CHFrameFile);

                    // Remove the original files (or not).
                    if (DeleteFlag) { File.Delete(FilesInDirectory[x]); }
                }

                // Combine contents of lists.
                List<byte> AllBytes = new List<byte>();
                for (int x = 0; x < IndividualFrameFiles.Count; x++) { AllBytes.AddRange(IndividualFrameFiles[x]); }

                CHFile = AllBytes.ToArray();
                ChipSpriteSheetAmountOfFrames = (ushort)IndividualFrameFiles.Count();

                GC.Collect();
            }
            else // If supplied a file sprite sheet.
            {
                // Convert the file to a DirectDrawSurface
                ConvertFileToDDS(FilePath);

                // Read in the newly generated .dds file.
                CHFile = File.ReadAllBytes(FilePath + ".dds");

                // Width is 256, fixed.
                ImageWidth = 256;

                // Get Image Height and from it the amount of frames.
                FourByteStorage[0] = CHFile[12 + 0]; FourByteStorage[1] = CHFile[12 + 1]; FourByteStorage[2] = CHFile[12 + 2]; FourByteStorage[3] = CHFile[12 + 3];
                ImageHeight = BitConverter.ToUInt16(FourByteStorage, 0);
                ChipSpriteSheetAmountOfFrames = (ushort)(ImageHeight / ImageWidth);

                // Remove the file header, we don't need it anymore.
                CHFile = CHFile.Skip(0x80).ToArray();

                // Remove the original file (or not).
                if (DeleteFlag) { File.Delete(FilePath); }
            }

            // File is now ready for manipulation.
            // ==> At this point, sprite sheet is once again present as a DDS.
            // Now we gotta put the file back into chunks.

            // Write the header of the ._CP file.
            TwoByteStorage = BitConverter.GetBytes(ChipSpriteSheetAmountOfFrames);
            CPFileList.Add(TwoByteStorage[0]); CPFileList.Add(TwoByteStorage[1]);

            /////
            /// Chunk List Is about to be filled.
            /////
            List<Byte[]> ChunkList = new List<byte[]>();

            ushort ChunkNumber = 0; // Chunk Code to be Appended to CPFileList
            uint ChunkPosition = 0; // Physical Byte where the Chunk is currently located.

            /// Here we must iterate through the entire image 16x16 and look for chunks with pixels.
            /// If there is a non zero item, save as a chunk.
            /// 

            // Identify all of the individual chunks.
            while ( ChunkPosition < CHFile.Length )
            {
                ReadChunk(ChunkPosition, ImageWidth, ChunkList, CHFile, MultiplierColourSpace);
                ChunkPosition = IncrementChunkPosition(ChunkPosition, ImageWidth, MultiplierColourSpace);
            }

            /// Reminder:
            /// CPFileList, Active ._CP file to write to.
            /// CHFileList, Active ._CH file to write chunks to (header already written at this point :).
            
            // Now that we have all of the chunks, we need to export the ones that are not completely empty into a new image.
            for (int x = 0; x < ChunkList.Count; x++) // Loop over every chunk
            {
                bool IsChunkEmpty = true; // This will be set to false if all pixels will have no ARGB values in chunk.

                foreach (byte ARGB4444 in ChunkList[x])
                {
                    if (ARGB4444 != 0)
                    {
                        IsChunkEmpty = false;
                        break; // Breaks out of parent Foreach
                    } // Set false if any pixel has an RGB value.
                }

                if (IsChunkEmpty == false) // If this whole chunk is not empty, copy the contents of it to the new file, CHFileList.
                {
                    CHFileList.AddRange(ChunkList[x]); // Append chunk to ._CH file.

                    // Append chunk to ._CP file.
                    CPFileList.AddRange(BitConverter.GetBytes(ChunkNumber));
                    ChunkNumber += 1;
                }
                else
                {
                    ushort EmptyChunkValue = 0xFFFF;
                    CPFileList.AddRange( BitConverter.GetBytes(EmptyChunkValue) );
                }
            }

            // Calculate Chunk Size and Number of Chunks and add to ._CH header, completing the file.
            int ChunkSize = (ChunkWidth * ChunkWidth) * MultiplierColourSpace;
            ChipSpriteAmountOfChunks = (ushort) (CHFileList.Count / ChunkSize);
            byte[] CHFileListHeader = BitConverter.GetBytes(ChipSpriteAmountOfChunks);
            CHFileList.InsertRange(0, CHFileListHeader);

            // Now it's done, just save and go :P

            // Get directory of file.
            FileDirectory = new FileInfo(FilePath).Directory.FullName;

            // Obtain the File Name of the File.
            FileName = FilePath;
            if (FilePath.Length > FileDirectory.Length) { FileName = FilePath.Substring(FileDirectory.Length + 1); }
            // +1 compensates for resolved path

            if (IsDirectory == true)
            {
                Directory.CreateDirectory(FileDirectory + @"\" + FileName + @"\" + "Output" + @"\");
                string SaveCHPath = FileDirectory + @"\" + FileName + @"\" + "Output" + @"\" + FileName + " ._CH";
                string SaveCPPath = FileDirectory + @"\" + FileName + @"\" + "Output" + @"\" + FileName + "P._CP";
                File.WriteAllBytes(SaveCHPath, CHFileList.ToArray());
                File.WriteAllBytes(SaveCPPath, CPFileList.ToArray());
            }
            else
            {
                string SaveCHPath = FileDirectory + @"\" + FileName.Substring(0, FileName.Length - 10) + "._CH";
                string SaveCPPath = FileDirectory + @"\" + FileName.Substring(0, FileName.Length - 11) + "P._CP";
                File.WriteAllBytes(SaveCHPath, CHFileList.ToArray());
                File.WriteAllBytes(SaveCPPath, CPFileList.ToArray());
            }
        }

        // Draws a 16x16 chunk at a specified physical position
        private static void ReadChunk(uint Position, uint ImageWidth, List<byte[]> ChunkList, byte[] CHImageSource, uint BytesPerPixel)
        {
            byte[] Chunk = new byte[256 * BytesPerPixel]; // Allocate space for chunk.
            uint OriginalPosition = Position; // Save position of top left. 
            uint PositionInChunkToRead; // Where in the current chunk is the pointer.
            // Basically we will iterate over every pixel from top left to bottom right, left to right, up to down within certain regions of the original image, copying them to a chunk.

            for (int x = 0; x < ChunkWidth; x++) // For every pixel level/height in chunk.
            { // 1 indexed to prevent introducing another variable.
                for (int y = 0; y < ChunkWidth; y++) // Draw line of chunk.
                {
                    // 0,0 of Chunk | How far down Chunk | How far across Chunk.
                    Position = (uint)(OriginalPosition + (ImageWidth * x * BytesPerPixel) + (y * BytesPerPixel)); // Absolute position of pixel to draw in the new bitmap to be drawn.
                    PositionInChunkToRead = (uint)((y * BytesPerPixel) + (x * ChunkWidth * BytesPerPixel)); // Position in chunk to draw to be copied.
                    Chunk[PositionInChunkToRead] = CHImageSource[Position]; Chunk[PositionInChunkToRead + 1] = CHImageSource[Position + 1];  // In the end, read the chunk.
                }
            }

            ChunkList.Add(Chunk);
            Position = OriginalPosition; // Restore Original Position such that it may be re-used for incrementation.
        }

        // Draws a 16x16 chunk at a specified physical position
        private static void DrawChunk(uint Position, uint ImageWidth, byte[] ChunkToDraw, byte[] ImageToDrawOn, uint BytesPerPixel)
        {
            uint OriginalPosition = Position; // Save position of top left. 
            uint PositionInChunkToDraw; // Where in the current chunk is the pointer.
            // Basically we will iterate over every pixel from top left to bottom right, left to right, up to down.

            for (int x = 0; x < ChunkWidth; x++) // For every pixel level/height in chunk.
            { // 1 indexed to prevent introducing another variable.
                for (int y = 0; y < ChunkWidth; y++) // Draw line of chunk.
                {
                    // Absolute position of pixel to draw in the new bitmap to be drawn.
                    Position = (uint) (OriginalPosition + (ImageWidth * x * BytesPerPixel) + (y * BytesPerPixel) );
                    PositionInChunkToDraw = (uint)( (y*BytesPerPixel) + (x*ChunkWidth*BytesPerPixel)); // Position in chunk to draw to be copied.
                    ImageToDrawOn[Position] = ChunkToDraw[PositionInChunkToDraw]; ImageToDrawOn[Position + 1] = ChunkToDraw[PositionInChunkToDraw + 1];  // In the end, draw the chunk.
                }
            }

            Position = OriginalPosition; // Restore Original Position such that it may be re-used for incrementation.
        }

        // Sets next square to be drawn
        private static uint IncrementChunkPosition(uint Position, uint ImageWidth, uint BytesPerPixel)
        {
            uint NextHorizontalPosition = Position + (ChunkWidth * BytesPerPixel);
            uint HorizontalEndingPosition = ImageWidth * BytesPerPixel;

            if (Position == 0) { return Position + (ChunkWidth *BytesPerPixel); }
            else if ( (NextHorizontalPosition % HorizontalEndingPosition) != 0) { return Position + (ChunkWidth*BytesPerPixel); } // If not going to overflow the line width, move the line along.
            else // Else increment by one line;
            {
                uint ChunksPerWidth = (uint)(ImageWidth / ChunkWidth);
                ChunkLevel += 1;
                return ((ChunkWidth * ChunkWidth) * ChunksPerWidth) * BytesPerPixel * ChunkLevel;
            }
        }

        private static void ResetChunkPosition()
        { ChunkLevel = 0; }

        private static void LoadCPFile()
        {
            // This means that this code is of no use right now this very moment.
            FileInfo CPFileData = new FileInfo(FilePath.Substring(0, FilePath.Length - 5) + "P._CP");

            // Allocate enough memory, get and set amount of poses.
            try
            {
                CPFile = new byte[CPFileData.Length];
                CPFile = File.ReadAllBytes(FilePath.Substring(0, FilePath.Length - 5) + "P._CP");
                TwoByteStorage[0] = CPFile[0]; TwoByteStorage[1] = CPFile[1];
                ChipSpriteSheetAmountOfFrames = BitConverter.ToUInt16(TwoByteStorage, 0);
            }
            catch
            {
                Console.WriteLine("._CP File not found for the corresponsing ._CH sprite sheet.\n" +
                                  "If the file is not a sprite sheet, please rename the file to NOT start with `CH`.\n" +
                                  "If the file is a sprite sheet, make sure that the sprite sheet is of the correct name\n" +
                                  "Names are treated as if they were to directly come from ED6Unpacker\n" +
                                  "e.g. CH20000 ._CH & CH20000 P._CP\n" +
                                  "Failed File: " + FilePath.Substring(0, FilePath.Length - 5) + "P._CP");
                Console.WriteLine("Exiting Application...");
                Console.ReadLine();
                Environment.Exit(0);
            }
            // DONE HERE
        }

        // StartingByte = Byte to start copy operation at.
        private static void CopyBytesToArray(int BytesToCopy, int StartingByteSource, int StartingByteDestination, byte[] SourceArray, byte[] DestinationArray)
        {
            for (int z = 0; z < BytesToCopy; z++) // Only need of BytesToCopy
            {
                DestinationArray[StartingByteDestination + z] = SourceArray[StartingByteSource + z]; // Coooopy!
            }
        }

        // StartingByte = Byte to start copy operation at.
        private static void CopyBytesToList(int BytesToCopy, int StartingByteSource, int StartingByteDestination, List<byte> SourceArray, List<byte> DestinationArray)
        {
            for (int z = 0; z < BytesToCopy; z++) // Only need of BytesToCopy
            {
                DestinationArray[StartingByteDestination + z] = SourceArray[StartingByteSource + z]; // Coooopy!
            }
        }

        private static void SetColourProfile()
        {
            switch (ColorProfile)
            {
                case 1:
                    CHHeader = Properties.Resources.Header_u1555; HeaderName = "_u1555";
                    break;
                case 2:
                    CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444";
                    break;
                case 3:
                    CHHeader = Properties.Resources.Header_u8888; HeaderName = "_u8888";
                    break;
            }
        }

        private static void RunDetectionFilters()
        {
            int OriginalFileSizeKB = OriginalFileSize / 1024;
            if (FileName.StartsWith("H"))
            {
                if (OriginalFileSizeKB >= 1024)
                {
                    ImageHeight = 1024;
                    if (OriginalFileSizeKB >= 4096) { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                    else if (OriginalFileSizeKB >= 3072) { CHHeader = Properties.Resources.Header_u1555; HeaderName = "_u1555"; }
                    else if (OriginalFileSizeKB >= 2048) { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                    else { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                }
                else
                {
                    ImageHeight = 512;
                    if (FileName.StartsWith("HFACE")) { CHHeader = Properties.Resources.Header_u1555; HeaderName = "_u1555"; }
                    else { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }

                }
            }
            else
            {
                if (OriginalFileSizeKB >= 256)
                {
                    ImageHeight = 512;
                    if (OriginalFileSizeKB >= 1024) { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                    else if (OriginalFileSizeKB >= 768) { CHHeader = Properties.Resources.Header_u1555; HeaderName = "_u1555"; }
                    else if (OriginalFileSizeKB >= 512) { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                    else { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                }
                else if (OriginalFileSizeKB == 32)
                {
                    ImageHeight = 128;
                    CHHeader = Properties.Resources.Header_u1555; HeaderName = "_u1555";
                }
                else
                {
                    ImageHeight = 256;
                    if (FileName.StartsWith("BFACE") | FileName.StartsWith("CTI")) { CHHeader = Properties.Resources.Header_u1555; HeaderName = "_u1555"; }
                    else { CHHeader = Properties.Resources.Header_u4444; HeaderName = "_u4444"; }
                }
            }
        }

        
    }
}
