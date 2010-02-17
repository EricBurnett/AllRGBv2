// Copyright 2010 Eric Burnett, except where noted.
// Licensed for use under the LGPL (or others similar licenses on request).

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using KDTreeDLL;    

namespace AllRGBv2 {
    class Config {
        public string Path;
        public ColorSpace Cs;
        public int Bpc;
    };

    class AllRGBv2 {
        static void Main(string[] args) {
            Config c = tryParseArgs(args);
            if (c == null) {
                Console.Out.WriteLine("Usage: AllRGBv2 file.ext {bitsPerChannel} {color space}");
                Console.Out.WriteLine("EG: AllRGBv2 in.png 6 HSV");
                return;
            }
            allRGBify(c.Path, c.Bpc, c.Cs);
        }

        // Try to parse the command line arguments into an understandable
        // run configuration. Returns null if unsuccessful.
        static Config tryParseArgs(string[] args) {
            if (args.Length != 3) {
                return null;
            }
            Config c = new Config();
            c.Path = args[0];
            try {
                c.Bpc = Int32.Parse(args[1]);
            } catch (Exception) {
                return null;
            }

            string cs = args[2].ToUpperInvariant();
            if (cs == "RGB") {
                c.Cs = ColorSpace.RGB;
            } else if (cs == "HSL") {
                c.Cs = ColorSpace.HSL;
            } else if (cs == "HSV") {
                c.Cs = ColorSpace.HSV;
            } else {
                return null;
            }

            return c;
        }

        // Converts the specified image into an AllRGB version by looping
        // randomly through the source pixels and choosing the 'nearest'
        // remaining color to map it to. The number of color bits per channel
        // used dictates the size of the output image ([image]_allRGBv2.png). 
        // Uses the specified color space for coordinate locations.
        static void allRGBify(string path, int bitsPerChannel, ColorSpace cs) {
            if ((bitsPerChannel & 1) != 0) {
                Console.Out.WriteLine("bitsPerChannel must be divisible by 2");
                return;
            } else if (bitsPerChannel > 8) {
                Console.Out.WriteLine("Only up to 8 bits per channel are supported");
                return;
            }

            DateTime startTime = DateTime.Now;

            // imageSize = 2^(3*bitsPerChannel/2)
            int imageSize = 1 << (3 * bitsPerChannel >> 1);
            int numPixels = imageSize * imageSize;

            Bitmap bitmap = new Bitmap(Image.FromFile(path), imageSize, imageSize);
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, imageSize, imageSize),
                ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            // Load the raw pixel data, RGBRGBRGB..... Note that it may contain
            // padding at the end of rows; need to be very careful when indexing!
            int pixelBytes = bitmapData.Stride * bitmapData.Height;
            Debug.Assert(pixelBytes >= numPixels * 3);
            byte[] pixels = new byte[pixelBytes];
            System.Runtime.InteropServices.Marshal.Copy(
                bitmapData.Scan0, pixels, 0, pixelBytes);

            // A random ordering of the pixels to process.
            Pair<int, int>[] coords = getRandomPixelOrdering(imageSize, imageSize);

            // A KDTree of all the candidate colors the pixels can be mapped to.
            // We periodically need to rebuild this tree to keep it from getting
            // unbalanced.
            KDTree kd = buildKDTreeOfColors(bitsPerChannel, cs);
            int pixelsTillRebuild = coords.Length * 1 / 4;
            pixelsTillRebuild = Math.Min(pixelsTillRebuild, 1 << 19);

            for (int i = 0; i < coords.Length; ++i) {
                if ((i & ((1 << 7) - 1)) == 0) {
                    Console.Out.WriteLine("Done " + i + " pixels");

                    // If 'r' is caught, rebuild spatial index immediately.
                    if (Console.KeyAvailable) {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        switch (key.KeyChar) {
                            case 'r':
                                pixelsTillRebuild = 0;
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (pixelsTillRebuild == 0) {
                    rebuildKDTree(ref kd);
                    pixelsTillRebuild = Math.Max((coords.Length - i) * 1 / 4, 1000);
                    pixelsTillRebuild = Math.Min(pixelsTillRebuild, 1 << 19);
                }
                pixelsTillRebuild--;

                Pair<int, int> coord = coords[i];
                int pixelIndex = coord.First * bitmapData.Stride + 3 * coord.Second;
                byte oldR = pixels[pixelIndex];
                byte oldG = pixels[pixelIndex + 1];
                byte oldb = pixels[pixelIndex + 2];
                ColorLocation oldColor = new ColorLocation(oldR, oldG, oldb, cs);

                ColorLocation newColor = (ColorLocation)kd.nearest(oldColor.Location);
                kd.delete(newColor.Location);

                pixels[pixelIndex] = newColor.R;
                pixels[pixelIndex + 1] = newColor.G;
                pixels[pixelIndex + 2] = newColor.B;
            }

            // Finally, load the pixels back into the bitmap and save out.
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixelBytes);
            bitmap.UnlockBits(bitmapData);
            string newFileName =
                Path.GetDirectoryName(path) +
                Path.DirectorySeparatorChar +
                Path.GetFileNameWithoutExtension(path) +
                "_allRGBv2.png";
            bitmap.Save(newFileName, ImageFormat.Png);

            TimeSpan totalTime = DateTime.Now - startTime;
            Console.Out.WriteLine("Generating " + Path.GetFileName(newFileName) +
                                  " took " + totalTime.ToString());
        }

        // Create a random ordering of the pixels in an image.
        static Pair<int, int>[] getRandomPixelOrdering(int width, int height) {
            Pair<int, int>[] coords = new Pair<int, int>[width * height];
            for (int y = 0; y < height; ++y) {
                for (int x = 0; x < width; ++x) {
                    coords[y * width + x] = new Pair<int, int>(x, y);
                }
            }
            coords.Shuffle();
            return coords;
        }

        // Build a KDTree of all the possible colors, indexed by location in the
        // chosen color space. When fewer than 8 bits per pixel are used, the
        // low order bits are skipped within each channel.
        static KDTree buildKDTreeOfColors(int bitsPerChannel, ColorSpace cs) {
            int colorsPerChannel = 1 << bitsPerChannel;
            int shift = 8 - bitsPerChannel;

            ColorLocation[] colors = new ColorLocation[colorsPerChannel * colorsPerChannel * colorsPerChannel];
            int colorIndex = 0;
            for (int r = 0; r < colorsPerChannel; ++r) {
                for (int g = 0; g < colorsPerChannel; ++g) {
                    for (int b = 0; b < colorsPerChannel; ++b) {
                        ColorLocation c = new ColorLocation((byte)(r << shift),
                                              (byte)(g << shift),
                                              (byte)(b << shift),
                                              cs);
                        colors[colorIndex] = c;
                        colorIndex++;
                    }
                }
            }
            colors.Shuffle();
            KDTree kd = new KDTree();
            for (int i = 0; i < colors.Length; ++i) {
                ColorLocation c = colors[i];
                kd.insert(c.Location, c);
            }
            colors = null;
            return kd;
        }

        // Rebuild a KDTree by taking every item and inserting it into a new
        // tree. Takes a reference so it can try to garbage collect the old
        // tree before the new one is built.
        static void rebuildKDTree(ref KDTree kd) {
            Object[] objs = allInKDTree(ref kd);
            objs.Shuffle();
            Console.Out.WriteLine("Rebuilding KD tree with " + objs.Length + " items");
            kd = new KDTree();
            GC.Collect();
            for (int i = 0; i < objs.Length; ++i) {
                kd.insert(((ColorLocation)objs[i]).Location, objs[i]);
            }
        }

        // Returns an array of all the objects in a KDTree.
        static Object[] allInKDTree(ref KDTree kd) {
            double[] low = new double[3];
            double[] high = new double[3];
            for (int i = 0; i < 3; ++i) {
                low[i] = Double.NegativeInfinity;
                high[i] = Double.PositiveInfinity;
            }
            return kd.range(low, high);
        }
    }
}