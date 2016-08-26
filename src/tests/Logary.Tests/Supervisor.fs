module Logary.Tests.Supervisor

open Logary
open Hopac
open Fuchu

[<Tests>]
let tests =
  testList "Supervisor" [
    testCase "create" <| fun _ ->
      Supervisor.create () |> ignore

    testCase "start and stop"
      let sup = Supervisor.create ()
      sup |> Supervisor.start |> run
      sup |> Supervisor.stop |> run
  ]