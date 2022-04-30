using static chartTool_CSharp.dbFunctions;

namespace chartTool_CSharp
{
    class Program
    {
        static void Main(String[] args)
        {
            // Write out tool info
            Console.WriteLine(
                "{0}\r\n{1}",
                "chartTool (C#)",
                "Decrypt Fer.Al charts_shared .db files");

            // Verify args
            if (args.Length < 1)
            {
                Usage();
                return;
            }

            // Assign arg to var
            String dbPath = args[0];
            // Check if file exists
            if (!File.Exists(dbPath))
            {
                Console.WriteLine("Error: database does not exist");
                Usage();
                return;
            }


            // Init a copy file path
            String duplicateDBPath = dbPath.Replace(".db", "_decrypted.db");

            // Create a copy of the database to work on
            try
            {
                File.Copy(dbPath, duplicateDBPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
                return;
            }


            // Activate mode
            dbDecrypt(duplicateDBPath);

            // Check check
            Console.WriteLine("All done!");

        }

        static void Usage()
        {
            Console.WriteLine(
                    "{0}:\r\n\t{1}",
                    "Usage",
                    @"Decrypt example: chartTool C:\charts_shared.VERSION.db"
                    );
            return;
        }


    }
}