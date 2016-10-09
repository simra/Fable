[<NUnit.Framework.TestFixture>] 
module Fable.Tests.Json
open NUnit.Framework
open Fable.Tests.Util

#if FABLE_COMPILER
let inline toJson x = Fable.Core.Serialize.toJson x
let inline ofJson<'T> x = Fable.Core.Serialize.ofJson<'T> x
#else
let toJson = Newtonsoft.Json.JsonConvert.SerializeObject
let ofJson<'T> = Newtonsoft.Json.JsonConvert.DeserializeObject<'T>
#endif

type Child =
    { a: string
      b: int }

type Simple = {
    Name : string
    Child : Child
}

type U =
    | CaseA of int
    | CaseB of Simple list

[<Test>]
let ``Simple json - Records``() =
    let json = 
        """
        {
            "Name": "foo",
            "Child": {
                "a": "Hi",
                "b": 10
            }
        }
        """
    let result: Simple = ofJson json
    result.Name |> equal "foo"
    // Use the built in compare to ensure the fields are being hooked up.
    // Should compile to something like: result.Child.Equals(new Child("Hi", 10))
    result.Child = {a="Hi"; b=10} |> equal true  

[<Test>]
let ``Simple json - Unions``() =
    let u = CaseB [{Name="Sarah";Child={a="John";b=14}}]
    toJson u |> ofJson<U> |> (=) u |> equal true
    """{"Case":"CaseB","Fields":[[{"Name":"Sarah","Child":{"a":"John","b":14}}]]}"""
    |> ofJson<U> |> (=) u |> equal true

[<Test>] 
let ``Simple json - Date``() =
    let d = System.DateTime(2016, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
    let json = d |> toJson
    let result : System.DateTime = ofJson json
    result.Year |> equal 2016

type JsonDate = {  
    Date : System.DateTime
}
        
[<Test>] 
let ``Simple json - Child Date``() =
    let d = System.DateTime(2016, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
    let json = { Date = d } |> toJson
    let result : JsonDate = ofJson json
    result.Date.Year |> equal 2016

type JsonArray = {
    Name : string
}

[<Test>] 
let ``Simple json - Array``() =
    let json = """[{ "Name": "a" }, { "Name": "b" }]"""
    let result : JsonArray[] = ofJson json
    result |> Array.length |> equal 2
    result.[1] = { Name="b" } |> equal true  

type ChildArray = {
    Children : JsonArray[]
}

[<Test>] 
let ``Simple json - Child Array``() =
    let json = """{ "Children": [{ "Name": "a" }, { "Name": "b" }] }"""
    let result : ChildArray = ofJson json
    result.Children |> Array.length |> equal 2
    result.Children.[1] = { Name="b" } |> equal true

[<Test>] 
let ``Simple json - String Generic List``() =
    let json = """["a","b"]"""
    let result : System.Collections.Generic.List<string> = ofJson json
    result.Count |> equal 2
    result.[1] |> equal "b"

[<Test>] 
let ``Simple json - Child Generic List``() =
    let json = """[{ "Name": "a" }, { "Name": "b" }]"""
    let result : System.Collections.Generic.List<JsonArray> = ofJson json
    result.Count |> equal 2
    result.[1] = { Name="b" } |> equal true  

[<Test>] 
let ``Simple json - List``() =
    let json = """["a","b"]"""
    let result : string list = ofJson json
    result |> List.length |> equal 2
    result.Tail |> List.length |> equal 1
    result.[1] |> equal "b"
    result.Head |> equal "a"


type ChildList = {
    Children : JsonArray list
}

[<Test>] 
let ``Simple json - Child List``() =
    let json = """{ "Children": [{ "Name": "a" }, { "Name": "b" }] }"""
    let result : ChildList = ofJson json
    result.Children |> List.length |> equal 2
    result.Children.[1] = { Name="b" } |> equal true

type Wrapper<'T> = { thing : 'T }

let inline parseAndUnwrap json: 'T = (ofJson<Wrapper<'T>> json).thing

[<Test>]
let ``Simple json - generic`` () =
    let result1 : string = parseAndUnwrap """ { "thing" : "a" } """
    result1 |> equal "a"
    let result2 : int = parseAndUnwrap """ { "thing" : 1 } """
    result2 |> equal 1
    let result3 : Child = parseAndUnwrap """ { "thing" : { "a": "a", "b": 1 } } """
    result3.a |> equal "a"
    result3 = {a = "a"; b = 1} |> equal true
    // let result4 : Child = parseAndUnwrap """ {"$type":"Fable.Tests.Json+Wrapper`1[[Fable.Tests.Json+Child, Fable.Tests]], Fable.Tests","thing":{"$type":"Fable.Tests.Json+Child, Fable.Tests","a":"a","b":1}} """
    // if result4 <> {a = "a"; b = 1} then
    //     invalidOp "things not equal" 

type OptionJson =
    { a: int option }

[<Test>]
let ``Simple json - Option Some`` () =
    // TODO: Deserialize also options as normal union cases?
    // let json = """ {"a":{"Case":"Some","Fields":[1]}} """
    let json1 = """ {"a":1 } """
    let result1 : OptionJson = ofJson json1
    let json2 = """ {"a":null } """
    let result2 : OptionJson = ofJson json2
    match result1.a, result2.a with
    | Some v, None -> v
    | _ -> -1
    |> equal 1

type TupleJson =
    { a: int * int }

[<Test>]
let ``Simple json - Tuple`` () =
    // TODO: Deserialize also tuples as objects?
    // let json = """ {"a":{"Item1":1,"Item2":2}} """
    let json = """ {"a":[1,2]} """
    let result : TupleJson = ofJson json
    result.a = (1, 2) |> equal true

type TupleComplexJson =
    { a: int * Child }

[<Test>]
let ``Simple json - Complex Tuple`` () =
    // TODO: Deserialize also tuples as objects?
    // let json = """ {"a":{"Item1":1,"Item2":{"a":"A","b":1}}} """
    let json = """ {"a":[1,{"a":"A","b":1}]} """
    let result : TupleComplexJson = ofJson json
    snd result.a = { a = "A"; b = 1 } |> equal true

type SetJson =
    { a: Set<string> }

[<Test>]
let ``Simple json - Set`` () =
    let json = """ {"a":["a","b"]} """
    let result : SetJson = ofJson json
    result.a |> Set.contains "b" |> equal true

type MapJson =
    { a: Map<string, Child> }

[<Test>]
let ``Simple json - Map`` () =
    let json = """ {"a":{"a":{"a":"aa","b":1},"b":{"a":"bb","b":2}}} """
    let result : MapJson = ofJson json
    result.a.Count |> equal 2
    result.a.["b"] = { a="bb"; b=2 } |> equal true
    
type DictionaryJson =
    { a: System.Collections.Generic.Dictionary<string, Child> }

[<Test>]
let ``Simple json - Dictionary`` () =
    let json = """ {"a":{"a":{"a":"aa","b":1},"b":{"a":"bb","b":2}}} """
    let result : DictionaryJson = ofJson json
    result.a.Count |> equal 2
    result.a.["b"] = { a="bb"; b=2 } |> equal true