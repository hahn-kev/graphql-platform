using System.Text.Json;
using HotChocolate.Execution.Configuration;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Skimmed;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using InputObjectType = HotChocolate.Skimmed.InputObjectType;
using ObjectType = HotChocolate.Skimmed.ObjectType;
using TypeKind = HotChocolate.Skimmed.TypeKind;

namespace HotChocolate.OpenApi;

public static class ServiceCollectionExtension
{
    public static IRequestExecutorBuilder AddOpenApi(
        this IRequestExecutorBuilder requestExecutorBuilder,
        string openApi,
        Action<HttpClient>? configureClient = null)
    {
        var document = new OpenApiStringReader().Read(openApi, out _);
        requestExecutorBuilder.ParseAndAddTypes(document);
        requestExecutorBuilder.AddHttpClient(configureClient);
        return requestExecutorBuilder;
    }

    public static IRequestExecutorBuilder AddOpenApi(
        this IRequestExecutorBuilder requestExecutorBuilder,
        Stream openApiStream,
        Action<HttpClient>? configureClient = null)
    {
        var document = new OpenApiStreamReader().Read(openApiStream, out _);
        requestExecutorBuilder.ParseAndAddTypes(document);
        requestExecutorBuilder.AddHttpClient(configureClient);
        return requestExecutorBuilder;
    }

    private static IRequestExecutorBuilder AddHttpClient(
        this IRequestExecutorBuilder requestExecutorBuilder,
        Action<HttpClient>? configureClient)
    {
        requestExecutorBuilder.Services.AddHttpClient("OpenApi", configureClient ?? (_ =>{}));
        return requestExecutorBuilder;
    }

    private static void ParseAndAddTypes(this IRequestExecutorBuilder requestExecutorBuilder,
        OpenApiDocument apiDocument)
    {
        requestExecutorBuilder.AddJsonSupport();
        requestExecutorBuilder.InitializeSchema(new OpenApiWrapper().Wrap(apiDocument));
    }

    private static void InitializeSchema(
        this IRequestExecutorBuilder requestExecutorBuilder,
        Skimmed.Schema schema)
    {
        if (schema.QueryType is { } queryType)
        {
            requestExecutorBuilder.AddQueryType(SetupType(queryType));
        }

        if (schema.MutationType is { } mutationType)
        {
            requestExecutorBuilder.AddMutationType(SetupType(mutationType));
        }

        foreach (var type in schema.Types.OfType<ObjectType>())
        {
            requestExecutorBuilder.AddObjectType(SetupType(type));
        }

        foreach (var type in schema.Types.OfType<InputObjectType>())
        {
            requestExecutorBuilder.AddInputObjectType(SetupInputType(type));
        }
    }

    private static Action<IObjectTypeDescriptor> SetupType(ComplexType skimmedType) =>
        desc =>
        {
            desc.Name(skimmedType.Name)
                .Description(skimmedType.Description);

            foreach (var field in skimmedType.Fields)
            {
                var fieldDescriptor = desc.Field(field.Name)
                    .Description(field.Description)
                    .Type(field.Type.Kind == TypeKind.List
                        ? new ListTypeNode(new NamedTypeNode(field.Type.NamedType().Name))
                        : new NamedTypeNode(field.Type.NamedType().Name));

                foreach (var fieldArgument in field.Arguments)
                {
                    fieldDescriptor.Argument(fieldArgument.Name, descriptor => descriptor
                        .Type(new NamedTypeNode(fieldArgument.Type.NamedType().Name)));
                }

                if (field.ContextData.TryGetValue("resolver", out var res) &&
                    res is Func<IResolverContext, Task<string>> resolver)
                {
                    fieldDescriptor.Resolve(async ctx =>
                    {
                        var value = await resolver.Invoke(ctx);
                        return JsonDocument.Parse(value).RootElement;
                    });
                }
                else
                {
                    fieldDescriptor.FromJson();
                }
            }
        };

    private static Action<IInputObjectTypeDescriptor> SetupInputType(InputObjectType skimmedType) =>
        desc =>
        {
            desc.Name(skimmedType.Name)
                .Description(skimmedType.Description);

            foreach (var field in skimmedType.Fields)
            {
                desc.Field(field.Name)
                    .Description(field.Description)
                    .Type(field.Type.Kind == TypeKind.List
                        ? new ListTypeNode(new NamedTypeNode(field.Type.NamedType().Name))
                        : new NamedTypeNode(field.Type.NamedType().Name));
            }
        };
}
