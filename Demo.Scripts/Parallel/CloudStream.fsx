﻿#load "../../packages/MBrace.Runtime.0.5.10-alpha/bootstrap.fsx"

open Nessos.MBrace
open Nessos.MBrace.Client

#nowarn "444"

#I "../../bin/"
#r "Demo.Lib.dll"
open Demo.Lib

#r "../../packages/Streams.0.2.3/lib/Streams.Core.dll"
#r "../../packages/Streams.Cloud.0.2.0-alpha/lib/Streams.Cloud.dll"

open Nessos.Streams
open Nessos.Streams.Cloud


#load "wordcount.fsx"
open Wordcount

open System
open System.IO


let getWordCount takeCount (data : ICloudArray<string>) =
    data
    |> CloudStream.ofCloudArray
    |> CloudStream.collect (fun line -> 
        WordCount.splitWords line 
        |> Stream.ofArray
        |> Stream.map (fun word -> word.ToLower())
        |> Stream.map (fun word -> word.Trim()))
    |> CloudStream.filter (fun word -> word.Length > 3)
    |> CloudStream.filter (not << WordCount.isNoiseWord)
    |> CloudStream.countBy id
    |> CloudStream.sortBy (fun (_,c) -> -c) takeCount
    |> CloudStream.toCloudArray


let runtime = MBrace.InitLocal(totalNodes = 4)

// create CloudArray of input data
let client = runtime.GetStoreClient()
let files = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\data\Shakespeare")
            |> Directory.EnumerateFiles
let data = client.CreateCloudArray("wordcount", files |> Seq.collect(fun f -> File.ReadLines(f)))


let proc = runtime.CreateProcess (getWordCount 20 data)

proc.AwaitResult() |> Seq.toArray


runtime.ShowProcessInfo()