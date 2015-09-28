using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Utils;

namespace Com.Schema
{
    public interface DcWorkspace : DcJson
    {
        ObservableCollection<DcSchema> Schemas { get; set; }

        void AddSchema(DcSchema schema);
        void RemoveSchema(DcSchema schema);

        DcSchema GetSchema(string name);

        DcSchema Mashup { get; set; }
    }

}
