module internal OpenAPITypeProvider.Json.Parser

open System
open OpenAPIParser.Version3.Specification
open Newtonsoft.Json.Linq
open OpenAPITypeProvider

let private checkRequiredProperties (req:string list) (jObject:JObject) =
    let props = jObject.Properties() |> Seq.toList
    let propertyExist name = props |> List.exists (fun x -> x.Name = name && x.Value.Type <> JTokenType.Null)
    req |> List.iter (fun p ->
        if propertyExist p |> not then raise <| FormatException (sprintf "Property '%s' is required by schema definition, but not present in JSON or is null" p)
    )

let defaultDateFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"

let private isOneOfAllowed (allowedValues:string list) (value:string) =
    match allowedValues |> List.tryFind ((=) value) with
    | Some v -> value
    | None -> FormatException (sprintf "Invalid value %s - Enum must contain one of allowed values: %A" value allowedValues) |> raise

let rec parseForSchema createObj defaultTyp (schema:Schema) (json:JToken) =
    match schema with
    | Boolean -> json.Value<bool>() |> box
    | Integer Int32 -> json.Value<int32>() |> box
    | Integer Int64 -> json.Value<int64>() |> box
    | Number NumberFormat.Double -> json.Value<double>() |> box
    | Number NumberFormat.Float -> json.Value<float32>() |> box
    | String StringFormat.String 
    | String StringFormat.Binary 
    | String StringFormat.Password -> json.Value<string>() |> box
    | String StringFormat.Byte -> json.Value<byte>() |> box
    | String StringFormat.DateTime
    | String StringFormat.Date -> json.Value<DateTime>() |> box
    | String StringFormat.UUID -> json.Value<string>() |> Guid |> box
    | String (StringFormat.Enum values) -> json.Value<string>() |> isOneOfAllowed values |> box
    | Array itemsSchema ->
        let jArray = json :?> JArray
        let items = [ for x in jArray do yield parseForSchema createObj defaultTyp itemsSchema x ]
        let typ = itemsSchema |> Inference.getComplexType (fun _ -> defaultTyp)
        Reflection.ReflectiveListBuilder.BuildTypedList typ items |> box
    | Object (props, required) ->
        let jObject = json :?> JObject
        jObject |> checkRequiredProperties required
        props 
        |> Map.map (fun name schema -> 
            if required |> List.contains name then
                parseForSchema createObj defaultTyp schema (jObject.[name]) |> Some
            else if jObject.ContainsKey name then
                let typ = schema |> Inference.getComplexType (fun _ -> defaultTyp)
                if jObject.[name].Type = JTokenType.Null then None
                else
                    parseForSchema createObj defaultTyp schema (jObject.[name]) 
                    |> Reflection.some typ
                    |> Some
            else None
        )
        |> Map.map (fun _ v -> defaultArg v null)
        |> Map.toList
        |> createObj
        |> box