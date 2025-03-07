using System;
using System.Collections.ObjectModel;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.SQLite;
using System.Drawing;
using System.IO;

namespace FaceRecognition_.Net
{
    public class User
    {
        public string name { get; set; }
        public ImageSource image { get; set; }
        public Bitmap face { get; set; }
        public byte[] templates { get; set; }
    }
    public class DBManager
    {
        private SQLiteConnection conn;
        public DBManager()
        {
            // Initialize the connection string
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mydb");
            conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
        }
        public void Create()
        {
            conn.Open();
            string createTableQuery = "CREATE TABLE IF NOT EXISTS person (name TEXT, face BLOB, templates BLOB);";
            using (SQLiteCommand cmd = new SQLiteCommand(createTableQuery, conn))
            {
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public void Insert(User user)
        {
            conn.Open();
            byte[] faceBytes = ImageProcess.BitmapToPngByteArray(user.face);
            string insertQuery = "INSERT INTO person (name, face, templates) VALUES (@name, @face, @templates);";

            using (SQLiteCommand cmd = new SQLiteCommand(insertQuery, conn))
            {
                cmd.Parameters.AddWithValue("@name", user.name);
                cmd.Parameters.AddWithValue("@face", faceBytes); // Example empty byte array
                cmd.Parameters.AddWithValue("@templates", user.templates); // Example empty byte array
                cmd.ExecuteNonQuery();
            }

            conn.Close();
        }

        public ObservableCollection<User> Load()
        {
            conn.Open();
            string selectQuery = "SELECT * FROM person;";

            ObservableCollection<User> usersList = new ObservableCollection<User>();

            using (SQLiteCommand cmd = new SQLiteCommand(selectQuery, conn))
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    User user = new User
                    {
                        name = reader["name"].ToString(),
                        face = ImageProcess.ByteArrayToBitmap(reader["face"] as byte[]),  // Convert the byte array back to an image (implement ConvertToImage)
                        templates = reader["templates"] as byte[] // Assuming the template is stored as a byte array
                    };
                    user.image = ImageProcess.ConvertBitmapToImageSource(user.face);
                    usersList.Add(user);
                }
            }
            conn.Close();

            return usersList;
        }

        public void Delete(string name)
        {
            conn.Open();
            string deleteQuery = "DELETE FROM person WHERE name = @name;";

            using (SQLiteCommand cmd = new SQLiteCommand(deleteQuery, conn))
            {
                cmd.Parameters.AddWithValue("@name", name);  // Use the provided name parameter
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public void DeleteAll()
        {
            conn.Open();  // Open the connection
            string deleteQuery = "DELETE FROM person;";  // SQL to delete all rows

            using (SQLiteCommand cmd = new SQLiteCommand(deleteQuery, conn))
            {
                cmd.ExecuteNonQuery();  // Execute the query
            }
            conn.Close();
        }
    }
}
