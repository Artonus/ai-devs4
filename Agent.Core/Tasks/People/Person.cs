namespace Agent.Core.Tasks.People;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string BirthPlace { get; set; } = string.Empty;
    public string BirthCountry { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;

    public int BirthYear => BirthDate.Year;
}
