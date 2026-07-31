using Microsoft.Data.SqlClient;

try
{
    using SqlConnection connection = new("Server=localhost;Trusted_Connection=True;Encrypt=optional");
    using SqlCommand cmd = new("select top 10 [Name] from uk_app.dbo.rvxItems", connection);

    await connection.OpenAsync();

    using SqlDataReader reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        Console.WriteLine(reader.GetString(0));
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

Console.ReadKey();