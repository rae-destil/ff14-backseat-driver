using Lumina;
using Lumina.Excel;

var lumina = new Lumina.GameData("D:\\Apps\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn\\game\\sqpack");

var actionsSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.Action>();

Console.WriteLine($"{actionsSheet.GetRow(0)}");

foreach (var action in actionsSheet)
{
    if (action.Name.ToString().Contains("Infinite"))
    {
        Console.WriteLine("wow");
    }
}
