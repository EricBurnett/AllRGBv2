// Copyright 2010 Eric Burnett, except where noted.
// Licensed for use under the LGPL (or others similar licenses on request).

using System.Drawing;
using System;

namespace AllRGBv2 {
    public enum ColorSpace {
        RGB, HSL, HSV
    };

    // Map RGB colors to their coordinate location within a specified color
    // space.
    public class ColorLocation {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }
        private double X { get; set; }
        private double Y { get; set; }
        private double Z { get; set; }

        public ColorLocation(byte r, byte g, byte b, ColorSpace cs) {
            R = r;
            G = g;
            B = b;

            switch (cs) {
                case ColorSpace.RGB:
                    setLocationAsRGB();
                    break;
                case ColorSpace.HSL:
                    setLocationAsHSL();
                    break;
                case ColorSpace.HSV:
                    setLocationAsHSV();
                    break;
            }
        }

        public double[] Location {
            get {
                return new double[] { X, Y, Z };
            }
        }

        private void setLocationAsHSV() {
            Color c = Color.FromArgb(R, G, B);
            double theta = c.GetHue() * Math.PI / 180.0;
            double s = c.GetSaturation();
            X = Math.Cos(theta) * s;
            Y = Math.Sin(theta) * s;
            Z = c.GetBrightness();
        }

        private void setLocationAsHSL() {
            Color c = Color.FromArgb(R, G, B);
            double hsv_h = c.GetHue();
            double hsv_s = c.GetSaturation();
            double hsv_v = c.GetBrightness();
            double hsl_h;
            double hsl_s;
            double hsl_l;
            hsv_to_hsl(hsv_h, hsv_s, hsv_v, out hsl_h, out hsl_s, out hsl_l);
            double theta = hsl_h * Math.PI / 180.0;
            X = Math.Cos(theta) * hsl_s;
            Y = Math.Sin(theta) * hsl_s;
            Z = hsl_l;
        }

        private void setLocationAsRGB() {
            X = R;
            Y = G;
            Z = B;
        }

        // From http://ariya.blogspot.com/2008/07/converting-between-hsl-and-hsv.html
        private void hsv_to_hsl(double h, double s, double v,
                                out double hh, out double ss, out double ll) {
            hh = h;
            ll = (2.0 - s) * v;
            ss = s * v;
            ss /= (ll <= 1) ? (ll) : 2.0 - (ll);
            if (Double.IsNaN(ss)) {
                ss = 0;
            }
            ll /= 2.0;
        }
    };
}
