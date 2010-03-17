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
        private static double   DIAGONAL_WEIGHT     = Math.Sqrt(1 / 2.0);
        private static float    ERROR_CAP           = 40;
        private static double   ERROR_ATTENUATION   = 1;
        private static bool     USE_ERROR_DIFFUSION = true;

        static void Main(string[] args) {
            Config c = tryParseArgs(args);
            if (c == null) {
                Console.Out.WriteLine("Usage: AllRGBv2 file.ext {bitsPerChannel} {color space}");
                Console.Out.WriteLine("EG: AllRGBv2 in.png 6 HSV");
                return;
            }
            string mask = null; // "C:/Users/Eric Burnett/Desktop/package/3_mask.png";
            allRGBify(c.Path, mask, c.Bpc, c.Cs);
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
        static void allRGBify(string path, string maskPath, int bitsPerChannel, ColorSpace cs) {
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

            // Load the raw pixel data, BGRBGRBGR..... Note that it may contain
            // padding at the end of rows; need to be very careful when indexing!
            int pixelBytes = bitmapData.Stride * bitmapData.Height;
            Debug.Assert(pixelBytes >= numPixels * 3);
            byte[] pixels = new byte[pixelBytes];
            System.Runtime.InteropServices.Marshal.Copy(
                bitmapData.Scan0, pixels, 0, pixelBytes);

            // If available, load the pixel mask as well
            // True is high priority, false is low.
            bool[,] maskFlags = new bool[imageSize, imageSize];
            if (maskPath != null && maskPath != "") {

                Bitmap mask = new Bitmap(Image.FromFile(maskPath), imageSize, imageSize);

                for (int y = 0; y < imageSize; ++y) {
                    for (int x = 0; x < imageSize; ++x) {
                        if (mask.GetPixel(x, y).R == 0) {
                            maskFlags[y, x] = true;
                        }
                    }
                }
            }

            // Set up error diffusion, if it is turned on. At the same time,
            // calculate the average color of the image for the initial error
            // term.
            float[, ,] pixelError = null;   // To be added to the corresponding 
            // pixel's color to get the target
            // color for lookup. 
            // Note: Order is y,x,{R,G,B}.
            bool[,] donePixels = null;
            if (USE_ERROR_DIFFUSION) {
                donePixels = new bool[imageSize, imageSize];
                pixelError = new float[imageSize, imageSize, 3];

                initializeErrorDiffusion(pixelError, pixels, bitmapData.Stride, imageSize);
            }

            // A random ordering of the pixels to process.
            Pair<int, int>[] coords = getRandomPixelOrdering(maskFlags, imageSize, imageSize);

            // A KDTree of all the candidate colors the pixels can be mapped to.
            // We periodically need to rebuild this tree to keep it from getting
            // unbalanced.
            KDTree kd = buildKDTreeOfColors(bitsPerChannel, cs);
            int pixelsTillRebuild = coords.Length * 1 / 4;
            pixelsTillRebuild = Math.Min(pixelsTillRebuild, 1 << 19);

            // Actually look up the colours, writing back to the pixel array as
            // we go.
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
                int x = coord.First;
                int y = coord.Second;
                int pixelIndex = y * bitmapData.Stride + 3 * x;
                byte oldR;
                byte oldG;
                byte oldb;
                if (USE_ERROR_DIFFUSION) {
                    oldR = capToByte(pixels[pixelIndex + 2] +
                        pixelError[y, x, 0]);
                    oldG = capToByte(pixels[pixelIndex + 1] +
                        pixelError[y, x, 1]);
                    oldb = capToByte(pixels[pixelIndex + 0] +
                        pixelError[y, x, 2]);
                } else {
                    oldR = pixels[pixelIndex + 2];
                    oldG = pixels[pixelIndex + 1];
                    oldb = pixels[pixelIndex + 0];
                }
                ColorLocation oldColor = new ColorLocation(oldR, oldG, oldb, cs);

                ColorLocation newColor = (ColorLocation)kd.nearest(oldColor.Location);
                kd.delete(newColor.Location);

                if (USE_ERROR_DIFFUSION) {
                    pixelError[y, x, 0] += pixels[pixelIndex + 2] - newColor.R;
                    pixelError[y, x, 1] += pixels[pixelIndex + 1] - newColor.G;
                    pixelError[y, x, 2] += pixels[pixelIndex + 0] - newColor.B;
                    markPixelDoneAndSpreadError(donePixels, pixelError, imageSize, x, y);
                }

                pixels[pixelIndex + 2] = newColor.R;
                pixels[pixelIndex + 1] = newColor.G;
                pixels[pixelIndex + 0] = newColor.B;
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

        // Marks a pixel as done, and moves it's error to neighboring unfinished
        // pixels, weighted by position.
        static void markPixelDoneAndSpreadError(bool[,] donePixels,
                float[, ,] pixelError, int imageSize, int x, int y) {
            Debug.Assert(donePixels[y,x] == false);
            donePixels[y, x] = true;

            // Find the total weight of pixels that are candadites for receiving
            // a portion of the error.
            double totalWeight = 0;
            for (int testY = Math.Max(y - 1, 0);
                    testY <= Math.Min(y + 1, imageSize - 1);
                    testY += 1) {
                for (int testX = Math.Max(x - 1, 0);
                        testX <= Math.Min(x + 1, imageSize - 1);
                        testX += 1) {
                    if (donePixels[testY, testX]) continue;

                    double weight = 1;
                    if (testX != x && testY != y) {
                        weight = DIAGONAL_WEIGHT;
                    }
                    totalWeight += weight;
                }
            }

            // Move the error to any candidate neighbor pixels
            for (int testY = Math.Max(y - 1, 0);
                    testY <= Math.Min(y + 1, imageSize - 1);
                    testY += 1) {
                for (int testX = Math.Max(x - 1, 0);
                        testX <= Math.Min(x + 1, imageSize - 1);
                        testX += 1) {
                    if (donePixels[testY, testX]) continue;

                    double weight = 1;
                    if (testX != x && testY != y) {
                        // Pixel is diagonal from target
                        weight = DIAGONAL_WEIGHT;
                    }
                    pixelError[testY, testX, 0] +=
                        (float)(pixelError[y, x, 0] * ERROR_ATTENUATION *
                        weight / totalWeight);
                    pixelError[testY, testX, 1] +=
                        (float)(pixelError[y, x, 1] * ERROR_ATTENUATION *
                        weight / totalWeight);
                    pixelError[testY, testX, 2] +=
                        (float)(pixelError[y, x, 2] * ERROR_ATTENUATION *
                        weight / totalWeight);

                    pixelError[testY, testX, 0] =
                        Math.Max(pixelError[testY, testX, 0], -ERROR_CAP);
                    pixelError[testY, testX, 0] =
                        Math.Min(pixelError[testY, testX, 0], ERROR_CAP);

                    pixelError[testY, testX, 1] =
                        Math.Max(pixelError[testY, testX, 1], -ERROR_CAP);
                    pixelError[testY, testX, 1] =
                        Math.Min(pixelError[testY, testX, 1], ERROR_CAP);

                    pixelError[testY, testX, 2] =
                        Math.Max(pixelError[testY, testX, 2], -ERROR_CAP);
                    pixelError[testY, testX, 2] =
                        Math.Min(pixelError[testY, testX, 2], ERROR_CAP);

                }
            }

            pixelError[y, x, 0] = 0;
            pixelError[y, x, 1] = 0;
            pixelError[y, x, 2] = 0;
        }

        // Setup the initial error matrix with the deviation from the expected
        // average color of the image, neutral gray, to avoid systemic errors.
        static void initializeErrorDiffusion(float[, ,] error, byte[] pixels, 
                int stride, int imageSize) {
            int numPixels = imageSize * imageSize;

            // Calculate average color of image.
            long[] totalColor = new long[3];
            for (int y = 0; y < imageSize; ++y) {
                for (int x = 0; x < imageSize; ++x) {
                    int pixel = y * stride + x * 3;
                    totalColor[0] += pixels[pixel + 2];
                    totalColor[1] += pixels[pixel + 1];
                    totalColor[2] += pixels[pixel + 0];
                }
            }
            float[] averageColor = new float[3];
            averageColor[0] = (float) ((double)totalColor[0] / numPixels);
            averageColor[1] = (float) ((double)totalColor[1] / numPixels);
            averageColor[2] = (float) ((double)totalColor[2] / numPixels);

            Console.Out.WriteLine("Average image color: {0:f2}, {1:f2}, {2:f2}",
                averageColor[0], averageColor[1], averageColor[2]);

            // Error from the expected average color (neutral gray).
            float[] averageError = new float[3];
            averageError[0] = 127.5f - averageColor[0];
            averageError[1] = 127.5f - averageColor[1];
            averageError[2] = 127.5f - averageColor[2];
            Console.Out.WriteLine("Average pixel error: {0:f2}, {1:f2}, {2:f2}", 
                averageError[0], averageError[1], averageError[2]);

            // Initialize the error array using this error term for each pixel.
            for (int y = 0; y < imageSize; ++y) {
                for (int x = 0; x < imageSize; ++x) {
                    error[y, x, 0] = averageError[0];
                    error[y, x, 1] = averageError[1];
                    error[y, x, 2] = averageError[2];
                }
            }

            Console.Out.WriteLine("Error diffusion array initialized.");
        }

        // Create a random ordering of the pixels in an image.
        // All coordinates with a set mask flag will appear before those
        // without, but the order will be random within each region.
        static Pair<int, int>[] getRandomPixelOrdering(bool[,] mask, int width, int height) {
            Pair<int, int>[] coords = new Pair<int, int>[width * height];
            Pair<int, int>[] setCoords = new Pair<int, int>[width * height];
            Pair<int, int>[] unsetCoords = new Pair<int, int>[width * height];
            for (int y = 0; y < height; ++y) {
                for (int x = 0; x < width; ++x) {
                    if (mask[y,x]) {
                        setCoords[y * width + x] = new Pair<int, int>(x, y);
                    } else {
                        unsetCoords[y * width + x] = new Pair<int, int>(x, y);
                    }
                }
            }
            setCoords.Shuffle();
            unsetCoords.Shuffle();

            int usedCoords = 0;
            for (int i = 0; i < setCoords.Length; ++i) {
                if (setCoords[i] != null) {
                    coords[usedCoords] = setCoords[i];
                    usedCoords++;
                }
            }
            for (int i = 0; i < unsetCoords.Length; ++i) {
                if (unsetCoords[i] != null) {
                    coords[usedCoords] = unsetCoords[i];
                    usedCoords++;
                }
            }
            Debug.Assert(usedCoords == coords.Length);
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

        // Returns the target color value as a byte, capped to the range 
        // [0, 255].
        static byte capToByte(float f) {
            if (f < 0) return 0;
            if (f > 255) return 255;
            return (byte)f;
        }
    }
}
