﻿#load "WinPerfCounter.fs"

open System
open System.Text.RegularExpressions

open Logary.WinPerfCounter

let munge name =
  Regex.Replace(name, "[\./]", "_")

let gen () =

  let genComment (pcc : PCC) { instance = i } =
    sprintf
        "/// %s: %s
///
/// %s"
      pcc.CategoryName
      pcc.CategoryHelp
      (match i with
      | NotApplicable -> "This performance counter does not have instance based counters"
      | _             -> "This performance counter does not have non-instance based counters")

  let genModuleHeader (pcc : PCC) =
    sprintf """module ``%s`` =

  [<Literal>]
  let Category = "%s"

  let PCC = getPCC Category"""
      (munge pcc.CategoryName)
      pcc.CategoryName

  let genCounter pc =
    match toPC pc, pc with
    | Some osPC, { category = cat; counter = cnt; instance = NotApplicable } ->
      sprintf """  /// %s: %s
  let ``%s`` =
    %s"""
        osPC.CounterName osPC.CounterHelp osPC.CounterName
        (sprintf """{ category = "%s"; counter = "%s"; instance = NotApplicable }"""
            cat cnt)
    | mOsPc , { category = cat; counter = cnt } ->
      let help = match mOsPc with | None -> "" | Some osPC -> osPC.CounterHelp
      sprintf """  /// %s: %s
  let ``%s`` instance =
    %s"""
        cnt help cnt
        (sprintf """{ category = "%s"; counter = "%s"; instance = instance }"""
            cat cnt)

  let genCounters (counters : PerfCounter list) =
    counters
    |> List.map genCounter
    |> List.filter (not << String.IsNullOrWhiteSpace)
    |> fun ctrs -> String.Join("\n", ctrs)

  let genListing (counters : PerfCounter list) =
    match counters with
    | [] -> """  let allCounters = []"""
    | hc :: rest ->
      sprintf """
  let allCounters =
    [ ``%s``
%s
    ]"""
        hc.counter
        (rest
          |> List.map (fun { counter = c } -> "``" + c + "``")
          |> List.map (fun s -> "      " + s)
          |> fun ss -> String.Join("\n", ss))

  let genFileHeader () =
    """/// Copyright Henrik Feldt 2014. Part of the Logary source code.
/// An autogenerated file with all performance counters found on a Windows 8.1 system
module Logary.WinPerfCounters

open System
open System.Diagnostics

open Logary.WinPerfCounter"""

  getAllPCC ()
  |> List.map (fun pcc -> pcc, getInstances pcc)
  |> List.sortBy (fun (pcc, _) -> pcc.CategoryName)
  |> List.map (function
    | pcc, []        -> pcc, getCounters pcc NotApplicable
    | pcc, inst :: _ -> pcc, getCounters pcc inst)
  |> List.map (fun (pcc, (c :: _ as counters)) ->
    genComment pcc c + "\n"
    + (genModuleHeader pcc) + "\n"
    + (genCounters counters) + "\n"
    + (genListing counters) + "\n")
  |> fun modules ->
    genFileHeader () + "\n"
    + String.Join("\n", modules)

let write () =
  let contents = gen ()
  System.IO.File.WriteAllText("X:\\logary\\src\\Logary\\WinPerfCounters.fs",
                              contents, System.Text.Encoding.UTF8)

/// SynchronizationNuma: A nice help text
///
/// This performance counter does not have non-instance based counters
module ``SynchronizationNuma Example`` =

  [<Literal>]
  let Category = "SynchronizationNuma"

  let PCC = getPCC Category

  let instances () =
    PCC |> Option.fold (fun s pcc -> getInstances pcc) []

  let ``Exec. Resource no-Waits AcqShrdWaitForExcl/sec`` instance =
    toPC' Category "Exec. Resource no-Waits AcqShrdWaitForExcl/sec" instance

  let ``Exec. Resource Boost Excl. Owner/sec`` instance =
    toPC' Category "Exec. Resource Boost Excl. Owner/sec" instance

  // etc

  let allCounters =
    [ ``Exec. Resource no-Waits AcqShrdWaitForExcl/sec``
      ``Exec. Resource Boost Excl. Owner/sec``
      // etc
    ]

  let countersFor instance =
    allCounters
    |> List.map (fun f -> f instance)
    |> List.filter Option.isSome
    |> List.map Option.get

/// System: a nice help text here too
///
/// This performance counter does not have instance based counters
module ``System Example`` =

  [<Literal>]
  let Category = "System"

  let PCC = getPCC Category

  let ``File Read Operations/sec`` =
    { category = Category; counter = "File Read Operations/sec"; instance = NotApplicable }

  // etc

  let allCounters =
    [ ``File Read Operations/sec`` ]