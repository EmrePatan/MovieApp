using Microsoft.EntityFrameworkCore;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchPostgresFunctions
{
    [DbFunction(name: "least", IsBuiltIn = true, IsNullable = false)]
    public static int Least(int value1, int value2) => value1;

    [DbFunction(name: "least", IsBuiltIn = true, IsNullable = false)]
    public static int Least(int value1, int value2, int value3) => value1;
}
