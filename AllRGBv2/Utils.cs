// Copyright 2010 Eric Burnett, except where noted.
// Licensed for use under the LGPL (or others similar licenses on request).

using System;

namespace AllRGBv2 {
    public static class ExtensionMethods {
        // Modified version of array shuffle provided by ICR on Stack Overflow:
        // http://stackoverflow.com/questions/375351/most-efficient-way-to-randomly-sort-shuffle-a-list-of-integers-in-c
        public static void Shuffle<T>(this T[] array) {
            Random random = new Random();
            for (int i = 0; i < array.Length; i += 1) {
                int swapIndex = random.Next(i, array.Length);
                if (swapIndex != i) {
                    T temp = array[i];
                    array[i] = array[swapIndex];
                    array[swapIndex] = temp;
                }
            }
        }
    };


    // Pair kindly provided by smink on Stack Overflow:
    // http://stackoverflow.com/questions/166089/what-is-c-analog-of-c-stdpair
    public class Pair<T, U> {
        public Pair(T first, U second) {
            this.First = first;
            this.Second = second;
        }

        public T First { get; set; }
        public U Second { get; set; }
    };
}