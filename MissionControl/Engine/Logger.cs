using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Travelling
{
    public class Logger<T>
    {
        private List<T> entries = new List<T>();

        public void Add(T entry)
        {
            entries.Add(entry);
        }

        // Writes every entry in one go, so a failure leaves nothing half-written to repeat on retry.
        public void Flush(string filePath)
        {
            List<string> lines = entries.Select(entry => $"{entry}").ToList();
            File.AppendAllLines(filePath, lines);
            entries.Clear();
        }
    }
}
