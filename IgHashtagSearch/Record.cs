using System;

namespace IgHashtagSearch
{
    public class Record
    {
        public double Date { get; set; }
        public DateTime DateValue
        {
            get { return Date.FromTimestamp(); }
        }
        public string Name { get; set; }
        public string Url { get; set; }
        public bool Video { get; set; }

        public string Cursor { get; set; }

        public override string ToString()
        {
            return Date + ";" + (Video ? 1 : 0).ToString() + ";" + Name + ";" + Url + ";" + Cursor + ";";
        }
    }
}
