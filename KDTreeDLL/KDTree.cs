using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace KDTreeDLL
{
    /// <summary>
    /// This is an adaptation of the Java KDTree library implemented by Levy 
    /// and Heckel. This simplified version is written by Marco A. Alvarez
    /// 
    /// KDTree is a class supporting KD-tree insertion, deletion, equality
    /// search, range search, and nearest neighbor(s) using double-precision
    /// floating-point keys.  Splitting dimension is chosen naively, by
    /// depth modulo K.  Semantics are as follows:
    /// <UL>
    /// <LI> Two different keys containing identical numbers should retrieve the 
    ///      same value from a given KD-tree.  Therefore keys are cloned when a 
    ///      node is inserted.
    /// <BR><BR>
    /// <LI> As with Hashtables, values inserted into a KD-tree are <I>not</I>
    ///      cloned.  Modifying a value between insertion and retrieval will
    ///      therefore modify the value stored in the tree.
    /// </UL>
    /// 
    /// @author Simon Levy, Bjoern Heckel
    /// Translation by Marco A. Alvarez
    /// 
    /// This version modified by Eric Burnett to a) fix a range() bug,
    /// b) optimize for the 3-dimensional, single-nearest-point-only case,
    /// and c) implement branch pruning on delete().
    /// </summary>
    public class KDTree
    {

        // root of KD-tree
        private KDNode m_root;

        // count of nodes
        private int m_count;

        public KDTree()
        {
            m_root = null;
        }

        /** 
         * Insert a node in a KD-tree.  Uses algorithm translated from 352.ins.c of
         *
         *   <PRE>
         *   &#064;Book{GonnetBaezaYates1991,                                   
         *     author =    {G.H. Gonnet and R. Baeza-Yates},
         *     title =     {Handbook of Algorithms and Data Structures},
         *     publisher = {Addison-Wesley},
         *     year =      {1991}
         *   }
         *   </PRE>
         *
         * @param key key for KD-tree node
         * @param value value at that key
         *
         * @throws KeySizeException if key.length mismatches K
         * @throws KeyDuplicateException if key already in tree
         */
        public void insert(double[] key, Object value)
        {

            if (key.Length != 3)
            {
                throw new KeySizeException();
            }

            else try
                {
                    m_root = KDNode.ins(new Point3(key), value, m_root, 0);
                }

                catch (KeyDuplicateException e)
                {
                    throw e;
                }

            m_count++;
        }

        /** 
         * Find  KD-tree node whose key is identical to key.  Uses algorithm 
         * translated from 352.srch.c of Gonnet & Baeza-Yates.
         *
         * @param key key for KD-tree node
         *
         * @return object at key, or null if not found
         *
         * @throws KeySizeException if key.length mismatches K
         */
        public Object search(double[] key)
        {

            if (key.Length != 3)
            {
                throw new KeySizeException();
            }

            KDNode kd = KDNode.srch(new Point3(key), m_root);

            return (kd == null ? null : kd.v);
        }


        /** 
         * Delete a node from a KD-tree.  Instead of actually deleting node and
         * rebuilding tree, marks node as deleted.  Hence, it is up to the caller
         * to rebuild the tree as needed for efficiency.
         *
         * @param key key for KD-tree node
         *
         * @throws KeySizeException if key.length mismatches K
         * @throws KeyMissingException if no node in tree has key
         */
        public void delete(double[] key)
        {

            if (key.Length != 3)
            {
                throw new KeySizeException();
            }

            else
            {
                bool deleted = false;
                m_root = KDNode.delete(new Point3(key), m_root, 0, ref deleted);
                if (deleted == false) {
                    throw new KeyNotFoundException();
                }
                m_count--;
            }
        }

        /**
        * Find KD-tree node whose key is nearest neighbor to
        * key. Implements the Nearest Neighbor algorithm (Table 6.4) of
        *
        * <PRE>
        * &#064;techreport{AndrewMooreNearestNeighbor,
        *   author  = {Andrew Moore},
        *   title   = {An introductory tutorial on kd-trees},
        *   institution = {Robotics Institute, Carnegie Mellon University},
        *   year    = {1991},
        *   number  = {Technical Report No. 209, Computer Laboratory, 
        *              University of Cambridge},
        *   address = {Pittsburgh, PA}
        * }
        * </PRE>
        *
        * @param key key for KD-tree node
        *
        * @return object at node nearest to key, or null on failure
        *
        * @throws KeySizeException if key.length mismatches K

        */
        public Object nearest(double[] key) {
            if (key.Length != 3) {
                throw new KeySizeException();
            }

            // initial call is with infinite rectangle and max distance
            Rect3 hr = Rect3.infiniteHRect();
            double max_dist_sqd = Double.MaxValue;
            Point3 keyp = new Point3(key);
            KDNode best = null;
            double best_dist_sq = Double.MaxValue;
            KDNode.nnbr(m_root, keyp, hr, max_dist_sqd, 0, 
                        ref best, ref best_dist_sq, null);
            Debug.Assert(best_dist_sq != Double.MaxValue);
            Debug.Assert(best != null);
            return best.v;
        }

        /** 
         * Range search in a KD-tree.  Uses algorithm translated from
         * 352.range.c of Gonnet & Baeza-Yates.
         *
         * @param lowk lower-bounds for key
         * @param uppk upper-bounds for key
         *
         * @return array of Objects whose keys fall in range [lowk,uppk]
         *
         * @throws KeySizeException on mismatch among lowk.length, uppk.length, or K
         */
        public Object[] range(double[] lowk, double[] uppk)
        {

            if (lowk.Length != uppk.Length)
            {
                throw new KeySizeException();
            }

            else if (lowk.Length != 3)
            {
                throw new KeySizeException();
            }

            else
            {
                List<KDNode> v = new List<KDNode>();
                KDNode.rsearch(new Point3(lowk), new Point3(uppk),
                       m_root, 0, v);
                Object[] o = new Object[v.Count];
                for (int i = 0; i < v.Count; ++i)
                {
                    KDNode n = (KDNode)v[i];
                    o[i] = n.v;
                }
                return o;
            }
        }

        public String toString()
        {
            return m_root.toString(0);
        }



        /// <summary>
        /// K-D Tree node class
        /// </summary>
        class KDNode
        {
            // these are seen by KDTree
            protected Point3 k;
            public Object v;
            protected KDNode left, right;
            public bool deleted;

            // Method ins translated from 352.ins.c of Gonnet & Baeza-Yates
            public static KDNode ins(Point3 key, Object val, KDNode t, int lev)
            {
                if (t == null)
                {
                    t = new KDNode(key, val);
                }

                else if (key.equals(t.k))
                {

                    // "re-insert"
                    if (t.deleted)
                    {
                        t.deleted = false;
                        t.v = val;
                    }

                    else
                    {
                        throw (new KeyDuplicateException());
                    }
                }

                else if (key.coord(lev) > t.k.coord(lev))
                {
                    t.right = ins(key, val, t.right, (lev + 1) % 3);
                }
                else
                {
                    t.left = ins(key, val, t.left, (lev + 1) % 3);
                }

                return t;
            }


            // Method srch translated from 352.srch.c of Gonnet & Baeza-Yates
            public static KDNode srch(Point3 key, KDNode t)
            {

                for (int lev = 0; t != null; lev = (lev + 1) % 3)
                {

                    if (!t.deleted && key.equals(t.k))
                    {
                        return t;
                    }
                    else if (key.coord(lev) > t.k.coord(lev))
                    {
                        t = t.right;
                    }
                    else
                    {
                        t = t.left;
                    }
                }

                return null;
            }

            // Try to delete the specified key from the tree. If successful,
            // prunes the dead branches off. Returns the new KDNode at this
            // location (possibly null). Reports success or failure in
            // deleted.
            public static KDNode delete(Point3 key, KDNode t, int lev, ref bool deleted) {
                deleted = false;
                if (t == null) return null;
                if (!t.deleted && key.equals(t.k)) {
                    t.deleted = true;
                    deleted = true;
                } else if (key.coord(lev) > t.k.coord(lev)) {
                    t.right = delete(key, t.right, (lev + 1) % 3, ref deleted);
                } else {
                    t.left = delete(key, t.left, (lev + 1) % 3, ref deleted);
                }

                if (!t.deleted || t.left != null || t.right != null) {
                    return t;
                } else {
                    return null;
                }
            }

            // Method rsearch translated from 352.range.c of Gonnet & Baeza-Yates
            public static void rsearch(Point3 lowk, Point3 uppk, KDNode t, int lev, List<KDNode> v)
            {

                if (t == null) return;
                if (lowk.coord(lev) <= t.k.coord(lev))
                {
                    rsearch(lowk, uppk, t.left, (lev + 1) % 3, v);
                }
                int j;
                for (j = 0; j < 3 && lowk.coord(j) <= t.k.coord(j) &&
                     uppk.coord(j) >= t.k.coord(j); j++)
                    ;
                if (j == 3 && !t.deleted) v.Add(t);
                if (uppk.coord(lev) > t.k.coord(lev))
                {
                    rsearch(lowk, uppk, t.right, (lev + 1) % 3, v);
                }
            }

            // Method Nearest Neighbor from Andrew Moore's thesis. Numbered
            // comments are direct quotes from there. Step "SDL" is added to
            // make the algorithm work correctly.  NearestNeighborList solution
            // courtesy of Bjoern Heckel.
            // The nearest neighbor is returned in best, with distance 
            // sqrt(best_dist_sq). Tmp is a temporary point, passed around
            // as an optimization so it doesn't need to be recreated all the
            // time. Can be passed in as null by callers.
            public static void nnbr(KDNode kd, Point3 target, Rect3 hr,
                                  double max_dist_sqd, int lev,
                                  ref KDNode best, ref double best_dist_sq, 
                                  Point3 tmp)
            {

                // 1. if kd is empty then set dist-sqd to infinity and exit.
                if (kd == null)
                {
                    return;
                }

                if (tmp == null) {
                    tmp = new Point3();
                }

                // 2. s := split field of kd
                int s = lev % 3;

                // 3. pivot := dom-elt field of kd
                Point3 pivot = kd.k;
                double pivot_to_target = Point3.sqrDist(pivot, target);

                // 4. Cut hr into to sub-hyperrectangles left-hr and right-hr.
                //    The cut plane is through pivot and perpendicular to the s
                //    dimension.
                Rect3 left_hr = hr; // optimize by not cloning
                Rect3 right_hr = (Rect3)hr.clone();
                left_hr.max.setCoord(s, pivot.coord(s));
                right_hr.min.setCoord(s, pivot.coord(s));

                // 5. target-in-left := target_s <= pivot_s
                bool target_in_left = target.coord(s) < pivot.coord(s);

                KDNode nearer_kd;
                Rect3 nearer_hr;
                KDNode further_kd;
                Rect3 further_hr;

                // 6. if target-in-left then
                //    6.1. nearer-kd := left field of kd and nearer-hr := left-hr
                //    6.2. further-kd := right field of kd and further-hr := right-hr
                if (target_in_left)
                {
                    nearer_kd = kd.left;
                    nearer_hr = left_hr;
                    further_kd = kd.right;
                    further_hr = right_hr;
                }
                //
                // 7. if not target-in-left then
                //    7.1. nearer-kd := right field of kd and nearer-hr := right-hr
                //    7.2. further-kd := left field of kd and further-hr := left-hr
                else
                {
                    nearer_kd = kd.right;
                    nearer_hr = right_hr;
                    further_kd = kd.left;
                    further_hr = left_hr;
                }
                right_hr = null;

                // 8. Recursively call Nearest Neighbor with paramters
                //    (nearer-kd, target, nearer-hr, max-dist-sqd), storing the
                //    results in nearest and dist-sqd
                nnbr(nearer_kd, target, nearer_hr, max_dist_sqd, lev + 1, ref best, ref best_dist_sq, tmp);
                nearer_hr = null;
                KDNode nearest = best;
                double dist_sqd;
                dist_sqd = best_dist_sq;

                // 9. max-dist-sqd := minimum of max-dist-sqd and dist-sqd
                max_dist_sqd = Math.Min(max_dist_sqd, dist_sqd);

                // 10. A nearer point could only lie in further-kd if there were some
                //     part of further-hr within distance sqrt(max-dist-sqd) of
                //     target.  If this is the case then
                Point3 closest = further_hr.closest(target, tmp);
                if (Point3.sqrDist(closest, target) < max_dist_sqd)
                {
                    // 10.1 if (pivot-target)^2 < dist-sqd then
                    if (pivot_to_target < dist_sqd)
                    {
                        // 10.1.1 nearest := (pivot, range-elt field of kd)
                        nearest = kd;

                        // 10.1.2 dist-sqd = (pivot-target)^2
                        dist_sqd = pivot_to_target;

                        // add to nnl
                        if (!kd.deleted)
                        {
                            best = kd;
                            best_dist_sq = dist_sqd;
                        }

                        max_dist_sqd = best_dist_sq;
                    }

                    // 10.2 Recursively call Nearest Neighbor with parameters
                    //      (further-kd, target, further-hr, max-dist_sqd),
                    //      storing results in temp-nearest and temp-dist-sqd
                    nnbr(further_kd, target, further_hr, max_dist_sqd, lev + 1, ref best, ref best_dist_sq, tmp);
                }
            }


            // constructor is used only by class; other methods are static
            private KDNode(Point3 key, Object val)
            {

                k = key;
                v = val;
                left = null;
                right = null;
                deleted = false;
            }

            public String toString(int depth)
            {
                String s = k + "  " + v + (deleted ? "*" : "");
                if (left != null)
                {
                    s = s + "\n" + pad(depth) + "L " + left.toString(depth + 1);
                }
                if (right != null)
                {
                    s = s + "\n" + pad(depth) + "R " + right.toString(depth + 1);
                }
                return s;
            }

            private static String pad(int n)
            {
                String s = "";
                for (int i = 0; i < n; ++i)
                {
                    s += " ";
                }
                return s;
            }
        }
    }

}