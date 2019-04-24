using System;
using System.Collections.Generic;


namespace Examples
{
    static partial class Program
    {
        static void Main(string[] args)
        {

            TyphoonAPI hilApi = new TyphoonAPI("hil");
            TyphoonAPI schApi = new TyphoonAPI("schematic_editor");

            schApi.Call(
                "load",
                new Dictionary<string, object>
                    {
                        { "filename", "C:\\Users\\victor.maryama\\Downloads\\repos\\FroniusDebug2\\FroniusDebug2\\model.tse" },
                    }
            );

            schApi.Call(
                "compile",
                new Dictionary<string, object>
                {
                }
            );

            //hilApi.Call(
            //    "load_model",
            //    new Dictionary<string, object>
            //        {
            //            { "file", "C:\\Users\\victor.maryama\\Downloads\\repos\\FroniusDebug2\\FroniusDebug2\\model Target files\\model.cpd" },
            //            { "vhil_device", true },
            //        }
            //);

            hilApi.Call(
                "start_simulation",
                new Dictionary<string, object>
                {
                }
            );

            hilApi.Call(
                "set_scada_input_value",
                new Dictionary<string, object>
                    {
                        { "scadaInputName", "Inputa" },
                        { "value", 10 },
                    }
            );

            object value = hilApi.Call(
                "read_analog_signal",
                new Dictionary<string, object>
                    {
                        { "name", "Probe1" },
                    }
            );

            Console.WriteLine("Read value is: {0}", value);

            hilApi.Call(
                "stop_simulation",
                new Dictionary<string, object>
                {
                }
            );

            Console.WriteLine("Done! Press <ENTER> to exit...");
            Console.ReadLine();


        }
    }


}