﻿namespace FoodFile

open Microsoft.AspNetCore.Mvc
open Newtonsoft.Json
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open Elastic
open Types

type ProcessingResult<'TResult> = 
    | Error of string
    | Result of 'TResult

[<ApiController>]
[<Route("api/[controller]")>]
type LocalController () =
    inherit ControllerBase()

    [<HttpGet("{id}")>] // Jetzt wo hier string als return steht müssen wir ContentType JSon evtl manuell setzen.
    member __.Get([<FromQuery>] id:string array) : string = //Multiple
        Search.LocalSearch (Array.toList id)
        |> JsonConvert.SerializeObject
        // ToDo: Error Handling.

    [<HttpPost>]
    member __.Create([<FromBody>] body:Object ) : string =
        
        let inputAtoms = 
            (JArray.Parse
            >> fun parseResult -> parseResult.ToObject<Atom list>()
            ) (body.ToString())
        
        let idResult =
            (List.distinctBy (fun atom -> atom.EntityID)
            >> function
                | [atom] -> // Exactly one Element. Continue.
                    match atom.EntityID with
                    | "" -> // This must contain a creation otherwise we reject
                        inputAtoms
                        // Check if List Contains a (maximum one) Creation
                        |> List.filter(fun atom -> 
                            match atom.Information with
                            | Creation _ -> true
                            | _ -> false )
                        |> function
                            // No Creation.
                            | [] ->     Result (atom.EntityID)
                            // Creation exsists.
                            | [_] ->    if List.exists (fun (atom:Atom) -> (atom.AtomID<>"" || atom.Version>0)) inputAtoms
                                        then ( Error("When a new Entity is created, all related atoms must have an empty atom-ID and a Version <= 0.") )
                                        else ( Result(Types.newEntityID) )
                            | _ ->      Error ("There can only be one entity creation per request as all atoms must belong to the same entity.")
                    | entityID ->   if List.exists (fun (atom:Atom) -> (atom.AtomID="" || atom.Version>0)) inputAtoms
                                    then Error("All atoms belong to the same entity but there are atoms with a specific version but no atom ID.")
                                    else Result entityID
                | _ ->              Error ("All atoms must belong to the same entity.")
                ) inputAtoms

        // Create Entity Record
        let entityResult =
            (function
                | Error e -> Error e
                | Result entityID -> 
                    inputAtoms
                    |> List.map (fun atom -> 
                        let timestamp = Convert.ToInt32 ((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds)
                        match (atom.AtomID, atom.Version>0) with
                        | ("", false) -> {atom with EntityID=entityID; AtomID=newAtomID; Version=timestamp}
                        | (_, false) -> {atom with EntityID=entityID; Version=timestamp}
                        | _ -> {atom with EntityID=entityID})
                    |> fun atoms -> Result {ID=entityID; Atoms=atoms}
            ) idResult
        
        entityResult
        |> function
            | Error _ -> idResult
            | Result entity ->
                entity.ID
                |> Elastic.GetEntityLocal 
                |> function
                    | None -> WriteEntity entity
                    | Some searchResult -> 
                        (searchResult.Merge entity).Value // IDs Will/Must correspond
                        |> WriteEntity // Will overwrite the existing document.
                |> ignore 
                idResult
        // In any case, we will eventually output the idResult.
        |> JsonConvert.SerializeObject
        
        // ToDO: Error Handling.
