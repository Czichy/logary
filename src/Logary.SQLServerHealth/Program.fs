﻿module Program

open System.Data
open System.Data.SqlClient

open NodaTime

open Logary
open Logary.Targets
open Logary.Configuration

open Logary.Metrics

open Nessos.UnionArgParser

open SQLServerHealth

type Arguments =
  | Drive_Latency of DriveName
  | Database_Latency of DatabaseName
  | File_Latency of FullyQualifiedPath
  | Sampling_Period of int64
  | [<Mandatory>] Connection_String of string
with
  interface IArgParserTemplate with
    member x.Usage =
      match x with
      | Drive_Latency _ ->
        "E.g. 'C:' or 'D:' - usually all things on the drive have similar \
         performance metrics as it's the underlying device that sets the \
         constraints. Do not include the backslash in this name."
      | Database_Latency _ ->
        "E.g. 'MyDatabase'; the database inside SQL server that you want to \
         probe"
      | File_Latency _ ->
        "A single file is the lowest qualifier that gives unique latency results"
      | Sampling_Period _ ->
        "A sampling period is the interval time in milliseconds between calls \
         to the database"
      | Connection_String _ ->
        "The database to connect to"

let openConn connStr : IDbConnection =
  let c = new SqlConnection(connStr)
  c.Open()
  upcast c

let parse args =
  let parser  = UnionArgParser<Arguments>()
  let parse   = parser.Parse args
  let drives  = parse.PostProcessResults(<@ Drive_Latency @>, Drive)
  let files   = parse.PostProcessResults(<@ File_Latency @>, SingleFile)
  let period  = parse.TryPostProcessResult(<@ Sampling_Period @>,
                                          Duration.FromMilliseconds)
                |> Option.fold (fun _ t -> t) (Duration.FromMilliseconds(1000L))
  let connStr = parse.GetResult <@ Connection_String @>
  let conf    = { SQLServerHealth.empty with
                    latencyTargets = drives @ files
                    openConn       = fun () -> openConn connStr }

  period, conf

[<EntryPoint>]
let main args =
  let period, conf = parse args
  use logary =
    withLogary' "Logary.SQLServerHealth" (
      withTargets [
        Console.create Console.empty "console"
      ] >>
      withRules [
        Rule.createForTarget "console"
      ] >>
      withMetrics [
        SQLServerHealth.create conf "sql_server_health" period
      ])
  // TODO: add in TopShelf support
  System.Console.ReadKey true |> ignore
  0 // return an integer exit code
