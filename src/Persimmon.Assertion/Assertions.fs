﻿namespace Persimmon

open System
open Persimmon
open FSharp.Object.Diff

[<NoEquality; NoComparison>]
type Assert(differ: ObjectDiffer, visitor: CustomAssertionVisitor) =
  
  member __.equals (expected: 'T) (actual: 'T) =
    if IEnumerable.isIEnumerable typeof<'T> && IEnumerable.equal expected actual then pass ()
    elif expected = actual then pass ()
    else
      let node = differ.Compare(actual, expected)
      node.Visit(visitor)
      let diff = visitor.Diff
      if String.IsNullOrEmpty diff then
        sprintf "Expect: %A%sActual: %A" expected Environment.NewLine actual
      else diff
      |> fail

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Assert =

  let private includedPropertyNames = [
    "FullName"
  ]
  let private typ = typeof<Type>
  let private filteredTypeProperties =
    typ.GetProperties()
    |> Array.choose (fun x -> if List.exists ((=) x.Name) includedPropertyNames then None else Some x.Name)
  let private runtimeType = typ.GetType()
  let private filteredRuntimeTypeProperties =
    runtimeType.GetProperties()
    |> Array.choose (fun x -> if List.exists ((=) x.Name) includedPropertyNames then None else Some x.Name)

  let internal differ =
    let builder =
      ObjectDifferBuilder.StartBuilding()
        .Comparison.OfPrimitiveTypes()
        .ToTreatDefaultValuesAs(Assigned)
        .And()
        .Inclusion
        .Exclude()
        .PropertyNameOfType(typ, filteredTypeProperties)
        .And()
    // System.RuntimeType does not exist mono.
    if typ <> runtimeType then
      builder.Inclusion
        .Exclude()
        .PropertyNameOfType(runtimeType, filteredRuntimeTypeProperties) 
      |> ignore
    builder
      .Differs
      .Register({ new DifferFactory with
        member __.CreateDiffer(dispatcher, service) =
          IEnumerableDiffer(dispatcher, service, builder.Identity) :> Differ
      })
      .Build()

  let equals (expected: 'T) (actual: 'T) =
    Assert(differ, AssertionVisitor("expected", expected, "actual", actual)).equals expected actual
