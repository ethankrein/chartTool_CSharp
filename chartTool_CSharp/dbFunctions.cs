using System.Collections;
using System.Data;
using System.Text;
using Microsoft.Data.Sqlite;

namespace chartTool_CSharp
{
    class dbFunctions
    {
        static List<String> getTableNames(String filePath)
        {
            List<String> tableNames = new List<String>();

            using (SqliteConnection chartDB = new SqliteConnection())
            {
                chartDB.ConnectionString = @"DataSource=" + filePath;
                chartDB.Open();
                using (SqliteCommand cmd = new SqliteCommand())
                {
                    cmd.Connection = chartDB;
                    cmd.CommandText = "SELECT name FROM Sqlite_master WHERE type = 'table'";

                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        tableNames = reader.Cast<IDataRecord>()
                                            .Select(dr => dr.GetString(0))
                                            .ToList();
                    }
                }
            }
            return tableNames;
        }

        static void defIdXOR(String filePath, List<String> tableNames)
        {
            // Loop over tables and decrypt defId
            foreach (String table in tableNames)
            {
                // Skip over some tables because they're not encrypted
                if (table.Equals("sqlite_sequence") || table.Equals("DBSettings"))
                {
                    continue;
                }

                // Grab column defId from table
                using (SqliteConnection chartDB = new SqliteConnection())
                {
                    chartDB.ConnectionString = @"DataSource=" + filePath;
                    chartDB.Open();
                    using (SqliteCommand cmd = new SqliteCommand())
                    {
                        cmd.Connection = chartDB;

                        // Pull defIds and ids
                        cmd.CommandText = "SELECT defId, id FROM " + table;

                        // Create a hash table to store values once decrypted
                        Hashtable decrypted_defIds = new Hashtable();

                        // Read over ids and defIds
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Get encrypted defId from db
                                String encrypted_defId = reader.GetValue(0).ToString();
                                // Get id for using to update
                                String id = reader.GetValue(1).ToString();
                                // Convert encrypted defId from Base64
                                encrypted_defId = Encoding.UTF8.GetString(Convert.FromBase64String(encrypted_defId));
                                // Decrypt the defId
                                String decrypted_defId = xorString("key_id", encrypted_defId);
                                // Push id and decrypted defId to table
                                decrypted_defIds.Add(id, decrypted_defId);
                            }
                        }

                        // Loop over hash table
                        foreach (String id in decrypted_defIds.Keys)
                        {
                            cmd.CommandText = "UPDATE " + table + " SET defId='" + decrypted_defIds[id] + "' WHERE id='" + id + "'";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        static void allFieldXOR(String filePath, List<String> tableNames)
        {
            // Loop over tables and decrypt all fields using defIDs
            foreach (String table in tableNames)
            {
                // Skip over some tables because they're not encrypted
                if (table.Equals("sqlite_sequence") || table.Equals("DBSettings"))
                {
                    continue;
                }

                // db operations
                using (SqliteConnection chartDB = new SqliteConnection())
                {
                    chartDB.ConnectionString = @"DataSource=" + filePath;
                    chartDB.Open();
                    using (SqliteCommand cmd = new SqliteCommand())
                    {
                        cmd.Connection = chartDB;

                        // Pull defIds and ids
                        cmd.CommandText = "SELECT defId, id FROM " + table;

                        // Create a hash table to store values once decrypted
                        Hashtable xorKeys = new Hashtable();

                        // Read over ids and defIds
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Get defId and append "key_" before it bc thats what the debugger tells me to do
                                String xorKey = "key_" + reader.GetValue(0).ToString();
                                // Get id for using in hashtable
                                String id = reader.GetValue(1).ToString();
                                // Push id and xorKey to table
                                xorKeys.Add(id, xorKey);
                            }
                        }

                        // Get all columns in current table
                        cmd.CommandText = "PRAGMA table_info('" + table + "')";

                        // create a list for columns
                        List<String> columns = new List<String>();

                        // Read over columns and add them to columns array
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Get column name
                                String column = reader.GetValue("name").ToString();
                                // Add it to the list
                                columns.Add(column);
                            }
                        }

                        // Loop over hash table to get row by id
                        foreach (String id in xorKeys.Keys)
                        {
                            // Loop over columns to update each one in row
                            foreach (String column in columns)
                            {
                                // Don't touch id or defId columns
                                if (column.Equals("id") || column.Equals("defId"))
                                {
                                    continue;
                                }

                                // Select data in column from db
                                cmd.CommandText = "SELECT " + column + " FROM " + table + " WHERE id='" + id + "'";

                                // Init this
                                String encryptedValue;

                                // Some fields empty or null so skip that
                                using (SqliteDataReader reader = cmd.ExecuteReader())
                                {
                                    if (!reader.Read())
                                    {
                                        continue;
                                    }

                                    // Get data and assign it to var
                                    encryptedValue = reader.GetValue(0).ToString();
                                }

                                // Convert encrypted value from Base64
                                encryptedValue = Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));

                                // decrypt
                                String decryptedValue = xorString(xorKeys[id].ToString(), encryptedValue);

                                // Sanitize for Sql
                                decryptedValue = decryptedValue.Replace("'", "''");

                                // Update data with decrypted value
                                cmd.CommandText = String.Format("UPDATE {0} SET {1}='{2}' WHERE id='{3}'", table, column, decryptedValue, id);
                                cmd.ExecuteNonQuery();
                            }
                        }

                    }
                }
            }
        }

        public static void dbDecrypt(String filePath)
        {
            // get names of all tables
            List<String> tableNames = getTableNames(filePath);

            // Decrypt defIds because they are used to decrypt every other field
            defIdXOR(filePath, tableNames);

            // xor fields
            allFieldXOR(filePath, tableNames);

        }

        public static String xorString(String key, String defId)
        {
            // SB for easy safe string tings
            StringBuilder xord_defId = new StringBuilder();

            // Loop over encrypted data
            for (int i = 0; i < defId.Length; i++)
            {
                // Get our char to decrypt
                char encryptedChar = defId[i];
                // Use appropriate portion of key
                char keyChar = key[i % key.Length]; // Bound to key

                // Xor char with key and append to decrypted string
                xord_defId.Append((char)(encryptedChar ^ keyChar));
            }

            return xord_defId.ToString();
        }

        // I was gonna do this but nim calls
        static void dbEncrypt(String fileName)
        {

        }
    }
}