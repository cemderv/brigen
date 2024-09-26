using brigen.decl;

namespace brigen;

internal static class FunctionHelper
{
    public static void DetermineFuncParamIndices(IEnumerable<FunctionParamDecl> pars)
    {
        int index = 0;
        int cApiIndex = 0;

        foreach (FunctionParamDecl par in pars)
        {
            par.Index = index;
            par.IndexInCApi = cApiIndex;

            // If this is an array, increment the C-API index
            // an additional time, because we have to account for the
            // array size parameter.
            if (par.Type.IsArray)
                ++cApiIndex;

            ++index;
            ++cApiIndex;
        }
    }
}