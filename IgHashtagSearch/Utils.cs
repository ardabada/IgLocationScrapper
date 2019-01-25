using System;

namespace IgHashtagSearch
{
    public static class Utils
    {
        /// <summary>
        /// Converts Unix timestamp to datetime
        /// </summary>
        /// <param name="timestamp">Unix timestamp</param>
        /// <returns>Returns datetime from timestamp</returns>
        public static DateTime FromTimestamp(this double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }
        /// <summary>
        /// Converts datetime to Unix timestamp
        /// </summary>
        /// <param name="date">Datetime</param>
        /// <returns>Returns timestamp</returns>
        public static double ToTimestamp(this DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date - origin;
            return Math.Floor(diff.TotalSeconds);
        }
    }
}
