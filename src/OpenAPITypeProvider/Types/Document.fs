module OpenAPITypeProvider.Types.Document

open ProviderImplementation.ProvidedTypes
open OpenAPITypeProvider.Parser

let createType asm ns typeName (filePath:string) =
    let typ = ProvidedTypeDefinition(asm, ns, typeName, None, hideObjectMethods = true, nonNullable = true, isErased = true)
    
    let api = filePath |> Document.loadFromYamlFile

    // ctor
    ProvidedConstructor([], fun _ -> <@@ obj() @@>) |> typ.AddMember

    // version    
    let version = api.SpecificationVersion
    ProvidedProperty("Version", typeof<string>, (fun _ -> <@@ version @@>)) |> typ.AddMember

    // info object
    let info = Info.createType asm ns api.Info
    info |> typ.AddMember
    ProvidedProperty("Info", info, fun _ -> <@@ obj() @@>) |> typ.AddMember
    
    // components object
    if api.Components.IsSome then
        
        // Schemas
        let schemas = ProvidedTypeDefinition(asm, ns, "Schemas", None, hideObjectMethods = true, nonNullable = true, isErased = true)
        
        // Add non-object root types
        api.Components.Value.Schemas
        |> Map.map (Schema.getRootNonObjectTypes asm ns)
        |> Map.filter (fun _ v -> v.IsSome)
        |> Map.map (fun _ v -> v.Value)
        |> Map.iter (fun _ v -> schemas.AddMember v)

        schemas |> typ.AddMember
        ProvidedProperty("Schemas", schemas, isStatic = true) |> typ.AddMember

    typ