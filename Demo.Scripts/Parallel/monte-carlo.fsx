﻿#load "../../packages/MBrace.Runtime.0.5.4-alpha/bootstrap.fsx" 

open Nessos.MBrace
open Nessos.MBrace.Client

// Calculates pi using the Monte Carlo method

open System
open System.IO
open System.Numerics
open System.Security.Cryptography

type RandomNumberGenerator(bufsize : int) =
    let bufsize = bufsize * 4
    let buf = Array.zeroCreate<byte> bufsize
    let mutable i = 0
    let rng = new RNGCryptoServiceProvider()

    do rng.GetBytes buf

    member __.Next () =
        // buffer exhausted, refill
        if i = bufsize then
            do rng.GetBytes buf
            i <- 0

        let n = (int buf.[i]) ||| (int buf.[i+1] <<< 8) ||| (int buf.[i+2] <<< 16) ||| (int buf.[i+3] <<< 24)
        i <- i + 4
        n

// the classic monte carlo implementation
// take random points and see of they are inside the circle or not
let monteCarloPiWorker (iterations : bigint) =
//    let rng = new System.Random()
    let rng = new RandomNumberGenerator (bufsize = 256)

    // the actual test for a single point
    let inline checkNextSample () =
        let r = int64 Int32.MaxValue
        let x = int64 (rng.Next())
        let y = int64 (rng.Next())
        x * x + y * y <= r * r

    // iterates through native ints for performance
    let smallMonteCarloPiWorker (iterations : int) =
        let mutable acc = 0
        for i = 1 to iterations do
            if checkNextSample () then acc <- acc + 1
        acc

    let mutable acc = 0I
    let mutable remainingSamples = iterations

    while remainingSamples / bigint Int32.MaxValue > 0I do
        acc <- acc + bigint (smallMonteCarloPiWorker Int32.MaxValue)
        remainingSamples <- remainingSamples - bigint Int32.MaxValue

    acc <- acc + bigint (smallMonteCarloPiWorker (int remainingSamples))

    acc

let rec getDigits dividend divisor = seq {
    let div, rem = BigInteger.DivRem(dividend,divisor)
    yield div
    yield! getDigits (rem * 10I) divisor
}

[<Cloud>]
let calculatePi (iterations : bigint) (digits : int) : Cloud<string> =
    cloud {
        let! workers = Cloud.GetWorkerCount()
        let workers = bigint (2 * workers)
        let iterationsPerWorker = iterations / workers
        let rem = iterations % workers

        let runWorker iterations = cloud { return monteCarloPiWorker iterations }
        
        let! results = 
            [ 
                for i in 1I .. workers -> runWorker iterationsPerWorker

                if rem > 0I then yield runWorker rem
            ]
            |> Cloud.Parallel

        let sum = results |> Array.sum

        let a = 4I * sum 
        let b = iterations

        return getDigits a b
               |> Seq.take digits
               |> Seq.map string
               |> String.concat ""
    }

// test

let runtime = MBrace.InitLocal 6

// run for 1000000000 iterations and get 10 digits
runtime.Run <@ calculatePi 1000000000I 10 @>