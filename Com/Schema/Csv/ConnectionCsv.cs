﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Com.Schema.Csv
{
    /// <summary>
    /// Access to a (text) file. Connection is specified by file name.
    /// </summary>
    public class ConnectionCsv
    {
        // Various parameters for reading the file:
        public int SampleSize = 10;

        private CsvHelper.CsvReader csvReader;

        public string[] CurrentRecord { get { return csvReader.CurrentRecord; } }

        public void OpenReader(string filePath, bool hasHeaderRecord, string delimiter, string decimalSign, Encoding encoding)
        {
            //CultureInfo cultureInfo = System.Globalization.CultureInfo.CurrentCulture;
            CultureInfo cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
            cultureInfo.NumberFormat.NumberDecimalSeparator = decimalSign;

            // Open file
            System.IO.StreamReader textReader = File.OpenText(filePath);
            //System.IO.StreamReader textReader = new StreamReader(table.FilePath, table.Encoding);

            csvReader = new CsvHelper.CsvReader(textReader);

            csvReader.Configuration.HasHeaderRecord = hasHeaderRecord;
            csvReader.Configuration.Delimiter = delimiter;
            csvReader.Configuration.CultureInfo = cultureInfo;
            csvReader.Configuration.Encoding = encoding;

            // If header is present (parameter is true) then it will read first line and initialize column names from the first line (independent of whether these are names or values)
            // If header is not present (parameter is false) then it will position on the first line and make valid other structures. In particular, we can learn that column names are null.

            try
            {
                csvReader.Read();
            }
            catch (Exception e)
            {
            }
        }
        public void OpenReader(TableCsv table)
        {
            OpenReader(table.FilePath, table.HasHeaderRecord, table.Delimiter, table.CultureInfo.NumberFormat.NumberDecimalSeparator, table.Encoding);
        }

        public void CloseReader()
        {
            if (csvReader == null) return;
            csvReader.Dispose();
            csvReader = null;
        }

        public bool ReadNext()
        {
            return csvReader.Read();
        }

        public List<string> ReadColumns()
        {
            if (csvReader == null) return null;
            if (csvReader.Configuration.HasHeaderRecord)
            {
                string[] headers = new string[0];
                try
                {
                    headers = csvReader.FieldHeaders;
                }
                catch(Exception e)
                {
                    return null;
                }

                return headers.ToList();
            }
            else // No columns
            {
                var names = new List<string>();
                var rec = csvReader.CurrentRecord;
                if (rec != null)
                {
                    for (int f = 0; f < rec.Length; f++)
                    {
                        names.Add("Column " + (f + 1));
                    }
                }
                return names;
            }
        }

        public List<string[]> ReadSampleValues()
        {
            var sampleRows = new List<string[]>();

            for (int row = 0; row < SampleSize; row++)
            {
                var rec = csvReader.CurrentRecord;
                if (rec == null) break;

                sampleRows.Add(rec);

                if (!csvReader.Read()) break;
            }

            return sampleRows;
        }

        private CsvHelper.CsvWriter csvWriter;

        public void OpenWriter(TableCsv table)
        {
            // Open file
            //System.IO.StreamWriter textWriter = File.OpenWrite(table.FilePath);
            System.IO.StreamWriter textWriter = new StreamWriter(table.FilePath, false, table.Encoding);

            csvWriter = new CsvHelper.CsvWriter(textWriter);

            csvWriter.Configuration.HasHeaderRecord = table.HasHeaderRecord;
            csvWriter.Configuration.Delimiter = table.Delimiter;
            csvWriter.Configuration.CultureInfo = table.CultureInfo;
            csvWriter.Configuration.Encoding = table.Encoding;

            csvWriter.Configuration.QuoteAllFields = true;

        }

        public void CloseWriter()
        {
            if (csvWriter == null) return;
            csvWriter.Dispose();
            csvWriter = null;
        }

        public void WriteNext(string[] record)
        {
            //csvWriter.WriteRecord<string[]>(record);
            for (int i = 0; i < record.Length; i++)
            {
                string val = "";
                if (record[i] != null) val = record[i];

                csvWriter.WriteField(val);
            }

            csvWriter.NextRecord();
        }

    }

}
