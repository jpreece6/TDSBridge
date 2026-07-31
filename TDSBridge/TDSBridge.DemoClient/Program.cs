using Microsoft.Data.SqlClient;

try
{
    using SqlConnection connection =
        new(
            "Data Source=localhost,8118;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Application Name=vscode-mssql;Application Intent=ReadWrite;Command Timeout=60;Packet Size=4096");
    using SqlCommand command = new("select * from [AdventureWorks2025].[HumanResources].[Employee]", connection);
    
    await connection.OpenAsync();

    using SqlDataReader reader = command.ExecuteReader();


    while (await reader.ReadAsync())
    {
        Console.WriteLine(reader.GetString(1));
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

Console.ReadKey();