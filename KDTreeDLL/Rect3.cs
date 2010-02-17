using System;
using System.Collections.Generic;
using System.Text;

namespace KDTreeDLL
{
    class Rect3
    {
        public Point3 min;
        public Point3 max;

        protected Rect3()
        {
            min = new Point3();
            max = new Point3();
        }

        protected Rect3(Point3 vmin, Point3 vmax)
        {

            min = vmin.clone();
            max = vmax.clone();
        }

        public Rect3 clone()
        {

            return new Rect3(min, max);
        }

        // from Moore's eqn. 6.6
        public Point3 closest(Point3 t, Point3 tmp)
        {

            Point3 p = tmp;

            p.X = Math.Max(t.X, min.X);
            p.X = Math.Min(p.X, max.X);
            p.Y = Math.Max(t.Y, min.Y);
            p.Y = Math.Min(p.Y, max.Y);
            p.Z = Math.Max(t.Z, min.Z);
            p.Z = Math.Min(p.Z, max.Z);

            return p;
        }

        // used in initial conditions of KDTree.nearest()
        public static Rect3 infiniteHRect()
        {

            Point3 vmin = new Point3(Double.NegativeInfinity,
                                     Double.NegativeInfinity,
                                     Double.NegativeInfinity);
            Point3 vmax = new Point3(Double.PositiveInfinity,
                                     Double.PositiveInfinity,
                                     Double.PositiveInfinity);

            return new Rect3(vmin, vmax);
        }

        public String toString()
        {
            return min + "\n" + max + "\n";
        }

    }
}
