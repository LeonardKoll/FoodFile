﻿namespace FoodFile

open FSharp.Data
open Newtonsoft.Json
open Newtonsoft.Json.Linq

// DUMMY

(*
    This module retrieves participant data from FoodFile.org
    and handles in-Memory Caching (later)
*)

module Members =

    [<Literal>]
    let MembershipProvider = "https://localhost:5001/api/members/"
    // This instance can also be pure Trace. Then we do not have a participant ID.
    let thisInstance = Some({ID="2VIJP2"; Name="RealRetail"; API="https://localhost:5001/api/entities/local/"; Password="123"})

    (*
    let freshfruitfarmers = {ID="IK7TEO"; Name="FreshFruitFarmers"; API="https://localhost:5001/api/entities/local/"; Password="123"}
    let sugarsilo = {ID="UC2NRQ"; Name="SugarSilo"; API="https://localhost:5001/api/entities/local/"; Password="123"}
    let yummyjam = {ID="8X55N4"; Name="YummyJam"; API="https://localhost:5001/api/entities/local/"; Password="123"}
    let members = [freshfruitfarmers;sugarsilo;yummyjam]
    *)

    let GetMembersRemote = fun (memberIDs:string list) ->   
        memberIDs
        |> function
            | [] -> []
            | _ ->
                memberIDs
                |> List.map ( fun id -> "id=" + id )
                |> String.concat "&"
                |> (fun arguments -> 
                    let test = MembershipProvider + "Multiple?" + arguments
                    printfn "%s" test
                    test
                    )
                |> Http.RequestString //ToDo: Error Handling. Log errors somewhere end return empty list.
                |> JArray.Parse // Errors may also occur here.
                |> fun parsed -> parsed.ToObject<Member list>()

    let GetMemberAPIs = fun (memberIDs:string list) ->
        memberIDs
        |> GetMembersRemote
        |> List.map ( fun p -> p.API )
        |> List.zip memberIDs

    let ExtractMemberIDs = fun (entities:Entity list) ->
        entities
        |> List.fold (fun (state:string list) entity ->
            entity.InvolvedMembers @ state
            |> List.distinct) []

    let ExtractMembers = 
        ExtractMemberIDs >> GetMembersRemote