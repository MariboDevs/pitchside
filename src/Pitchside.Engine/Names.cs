namespace Pitchside.Engine;

/// <summary>Fictional player names. Lists are ordinary given names and surnames; no real footballer is intended.</summary>
public static class Names
{
    public static IReadOnlyList<string> FirstNames { get; } =
    [
        "Sipho", "Thabo", "Lwazi", "Bongani", "Kagiso", "Tumelo", "Sibusiso", "Andile", "Lethabo", "Musa",
        "Daniel", "Joshua", "Ethan", "Oliver", "Samuel", "Nathan", "Isaac", "Caleb", "Owen", "Rhys",
        "Mateo", "Lucas", "Diego", "Rafael", "Tomas", "Andres", "Joaquin", "Emilio", "Bruno", "Thiago",
        "Jonas", "Finn", "Lars", "Mikkel", "Anders", "Kasper", "Emil", "Noah", "Elias", "Leon",
        "Kwame", "Kofi", "Yaw", "Femi", "Tunde", "Chidi", "Emeka", "Obi", "Sefu", "Amani",
        "Hugo", "Luca", "Matteo", "Nico", "Theo", "Milan", "Aris", "Yusuf", "Idris", "Tariq",
    ];

    public static IReadOnlyList<string> Surnames { get; } =
    [
        "Nkosi", "Dlamini", "Mokoena", "Mahlangu", "Sithole", "Khumalo", "Mthembu", "Ndlovu", "Zulu", "Mabaso",
        "Hartley", "Whitfield", "Pembroke", "Ashworth", "Kendrick", "Lowell", "Marchant", "Thorne", "Redfern", "Calloway",
        "Ortega", "Salinas", "Montero", "Valdez", "Cabrera", "Pineda", "Aguirre", "Delgado", "Ibarra", "Escobar",
        "Lindqvist", "Holm", "Brandt", "Vestergaard", "Nyberg", "Dahl", "Eriksen", "Sandberg", "Lund", "Kjaer",
        "Mensah", "Boateng", "Adjei", "Okafor", "Adeyemi", "Nwosu", "Okonkwo", "Abara", "Mwangi", "Otieno",
        "Ferrand", "Moreau", "Vidal", "Baptiste", "Conti", "Rinaldi", "Marino", "Greco", "Haddad", "Rahimi",
        "Keller", "Brenner", "Voss", "Hartmann", "Steiner", "Novak", "Kovac", "Horvat", "Petrov", "Ivanov",
        "Ostrowski", "Wojcik", "Nowak", "Zielinski", "Tanaka", "Sato", "Park", "Kim", "Nguyen", "Tran",
    ];

    public static string Generate(Pcg32 rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return $"{rng.Pick(FirstNames)} {rng.Pick(Surnames)}";
    }
}
