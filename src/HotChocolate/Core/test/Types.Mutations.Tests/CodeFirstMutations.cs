using System;
using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Tests;
using Microsoft.Extensions.DependencyInjection;
using Snapshooter.Xunit;
using Xunit;

namespace HotChocolate.Types;

public class CodeFirstMutations
{
    [Fact]
    public async Task SimpleMutation_Inferred()
    {
        Snapshot.FullName();

        await new ServiceCollection()
            .AddGraphQL()
            .AddMutationType(d => 
            {
                d.Name("Mutation");
                d.Field("doSomething")
                    .Argument("a", a => a.Type<StringType>())
                    .Type<StringType>()
                    .Resolve("Abc");
            })
            .AddMutationConventions(
                new MutationConventionOptions
                {
                    ApplyToAllMutations = true
                })
            .ModifyOptions(o => o.StrictValidation = false)
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }
}
