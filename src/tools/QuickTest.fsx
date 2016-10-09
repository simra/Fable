// Use this template to make quick tests when adding new features to Fable.
// You must run a full build at least once (from repo root directory,
// type `sh build.sh` on OSX/Linux or just `build` on Windows). Then:
// - When making changes to Fable.Compiler run `build QuickFableCompilerTest`
// - When making changes to fable-core run `build QuickFableCoreTest`

// Please don't add this file to your commits

// Uncomment these lines if you need access to Fable.Core helpers:
#r "../fable/Fable.Core/npm/Fable.Core.dll"
open Fable.Core
open Fable.Core.JsInterop

let equal expected actual =
    let areEqual = expected = actual
    printfn "%A = %A > %b" expected actual areEqual
    if not areEqual then
        failwithf "Expected %A but got %A" expected actual 

// Write here the code you want to test,
// you can later put the code in a unit test.

// Example:
// Seq.except [2] [1; 3; 2] |> Seq.last |> equal 3
// Seq.except [2] [2; 4; 6] |> Seq.head |> equal 4
