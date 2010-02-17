using System;

namespace KDTreeDLL
{
    class Point3
    {
        public double X;
        public double Y;
        public double Z;

        public Point3() { }

        public Point3(double[] x)
        {
            X = x[0];
            Y = x[1];
            Z = x[2];
        }

        public Point3(double x, double y, double z) {
            X = x;
            Y = y;
            Z = z;
        }

        public Point3 clone()
        {
            return new Point3(X, Y, Z);
        }

        public double coord(int i) {
            switch (i) {
                case 0:
                    return X;
                case 1:
                    return Y;
                case 2:
                    return Z;
            }
            return 0;
        }

        public void setCoord(int i, double v) {
            switch (i) {
                case 0:
                    X = v;
                    return;
                case 1:
                    Y = v;
                    return;
                case 2:
                    Z = v;
                    return;
            }
        }

        public bool equals(Point3 p)
        {
            if (X != p.X || Y != p.Y || Z != p.Z) {
                return false;
            }

            return true;
        }

        public static double sqrDist(Point3 x, Point3 y)
        {

            double dist = 0;
            double tmp;

            tmp = x.X - y.X;
            dist += tmp*tmp;
            tmp = x.Y - y.Y;
            dist += tmp*tmp;
            tmp = x.Z - y.Z;
            dist += tmp*tmp;

            return dist;

        }

        public String toString()
        {
            String s = "" + X + " " + Y + " " + Z;
            return s;
        }
    }
}
