﻿//#define SAVE_PROGRESS

using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil.Rocks;
using OTA.Patcher.Organisers;

namespace OTA.Patcher
{
    /// <summary>
    /// This file is specifically for Vanilla hooks.
    /// When Terraria is released for multi-platforms our core server class will not be of service anymore, or atleast we can reuse the Packet code from vanilla.
    /// What this class will do is expose the hooks we need for plugins. E.g. OnPlayerJoined
    /// </summary>
    public partial class Injector
    {
        public TerrariaOrganiser Terraria;
        public APIOrganiser API;

        private void InitOrganisers()
        {
            Terraria = new TerrariaOrganiser(_asm);
            API = new APIOrganiser(_self);
        }

        /// <summary>
        /// Hooks Begin and End of Terraria.Main.[Draw/Update]
        /// </summary>
        [OTAPatch(SupportType.Client, "Hooking XNA events")]
        private void HookXNAEvents()
        {
            foreach (var mth in new string[] { "Draw", "Update", "UpdateClient" })
            {
                var method = Terraria.Main.Methods.Single(x => x.Name == mth);
                var begin = API.MainCallback.Methods.First(m => m.Name == "On" + mth + "Begin");
                var end = API.MainCallback.Methods.First(m => m.Name == "On" + mth + "End");

                var il = method.Body.GetILProcessor();
                var first = method.Body.Instructions.First();

                il.InsertBefore(first, il.Create(OpCodes.Call, _asm.MainModule.Import(begin)));

                //Before any exit point
                foreach (var ins in method.Body.Instructions.Where(x => x.OpCode == OpCodes.Ret).Reverse())
                {
                    il.InsertBefore(ins, il.Create(OpCodes.Call, _asm.MainModule.Import(end)));
                }
            }
        }

        [OTAPatch(SupportType.Server, "Changing the architecture to match OTA")]
        private void ChangeArchitecture()
        {
            _asm.MainModule.Attributes = _self.MainModule.Attributes;
            _asm.MainModule.Architecture = _self.MainModule.Architecture;
        }

        //        [ServerHook]
        //        private void TestNetSwitch()
        //        {
        //            var sendData = Terraria.NetMessage.Method("SendData");
        //            var switchs = sendData.Body.Instructions.First(x => x.OpCode == OpCodes.Switch);
        //        }

        //        [ServerHook]
        //        private void HookHardModeTileUpdates()
        //        {
        //            //Inject a custom PlaceTile, that returns a bool
        //            //If false, then return/break
        //
        //            var hardUpdateWorld = Terraria.WorldGen.Methods.Single(x => x.Name == "hardUpdateWorld");
        //            var cbkOnTileChange = Terraria.Import(API.WorldGenCallback.Methods.Single(x => x.Name == "OnHardModeTileUpdate"));
        //
        //            var setTiles = hardUpdateWorld.Body.Instructions.Where(x => x.OpCode == OpCodes.Stfld
        //                               && x.Operand is FieldReference
        //                               && (x.Operand as FieldReference).Name == "type")
        //                                    .ToArray();
        //
        //            var il = hardUpdateWorld.Body.GetILProcessor();
        //            foreach (var setFld in setTiles.Take(1))
        //            {
        //                var insInsertBefore = setFld.FindPreviousInstructionByOpCode(OpCodes.Ldsfld);
        //
        //                Instruction insTarget;
        //                il.InsertBefore(insInsertBefore, insTarget = il.Create(OpCodes.Call, cbkOnTileChange));
        //                insInsertBefore.ReplaceTransfer(insTarget, hardUpdateWorld);
        ////                il.InsertBefore(insInsertBefore, il.Create(OpCodes.Pop));
        //                il.InsertBefore(insInsertBefore, il.Create(OpCodes.Brfalse_S, insInsertBefore));
        //                il.InsertBefore(insInsertBefore, il.Create(OpCodes.Ret));
        //            }
        ////            foreach (var setFld in hardUpdateWorld.Body.Instructions.Where(x => x.OpCode==OpCodes.Pop && x.Previous.Operand == cbkOnTileChange).ToArray())
        ////            {
        ////                var target = setFld.Next;
        ////                il.InsertAfter(setFld, il.Create(OpCodes.Ret));
        ////                il.Replace(setFld, il.Create(OpCodes.Brfalse, target));
        ////            }
        //
        ////            hardUpdateWorld.Body.OptimizeMacros();
        //        }

        //        [ServerHook]
        //        private void HookHardModeTileUpdates()
        //        {
        //            //So far I can tell there are 3 different sections.
        //            //  1) the first WorldGen.PlaceTile, plus the first two Main.tile[....].type calls
        //            //          - use return
        //            //  2) The next two Main.tile[....].type
        //            //          - use continue
        //            //  3) The rest of the Main.tile[....].type calls that follow
        //            //          - grab the [flag] that is used in the while loop
        //            //          - if [flag] then break else continue
        //
        //            var hardUpdateWorld = Terraria.WorldGen.Methods.Single(x => x.Name == "hardUpdateWorld");
        //            var cbkOnTileChange = Terraria.Import(API.WorldGenCallback.Methods.Single(x => x.Name == "OnHardModeTileUpdate"));
        //
        //            var placeTiles = hardUpdateWorld.Body.Instructions.Where(x => x.OpCode == OpCodes.Call
        //                                         && x.Operand is MethodReference
        //                                         && (x.Operand as MethodReference).Name == "PlaceTile")
        //                        .ToArray();
        //            var setTiles = hardUpdateWorld.Body.Instructions.Where(x => x.OpCode == OpCodes.Stfld
        //                                       && x.Operand is FieldReference
        //                                       && (x.Operand as FieldReference).Name == "type")
        //                        .ToArray();
        //
        //            var il = hardUpdateWorld.Body.GetILProcessor();
        //
        //            //Inject a hook before each PlaceTile call
        //            foreach (var placeTile in placeTiles)
        //            {
        //                var start = placeTile.FindInstructionByOpCodeBefore(OpCodes.Ldarg_0);
        //                if (start == null) throw new InvalidOperationException("Cannot find start point of PlaceTile arguments");
        //
        //                var args = start.GetInstructionsUntil(OpCodes.Call);
        //
        //                //For the arguments we only care up until the first ldc.i4
        //                var idx = args.FindIndex(x => x.OpCode == OpCodes.Ldc_I4) + 1 /*Dont remove the one we want to keep (the tile type)*/;
        //                var totalToRemove = args.Count - idx;
        //                for (var x = idx; x < idx + totalToRemove; x++)
        //                {
        //                    args.RemoveAt(idx);
        //                }
        //
        //                foreach (var arg in args)
        //                {
        //                    var ins = arg.Clone();
        //                    il.InsertBefore(start, ins);
        //                }
        //                il.InsertBefore(start, il.Create(OpCodes.Call, cbkOnTileChange));
        //                il.InsertBefore(start, il.Create(OpCodes.Pop));
        //
        //                //Now handle the result of the hook
        //
        //            }
        //
        //            //Inject a hook before each PlaceTile call
        //            foreach (var setTile in setTiles)
        //            {
        //                var start = setTile.FindInstructionByOpCodeBefore(OpCodes.Ldsfld);
        //                if (start == null) throw new InvalidOperationException("Cannot find start point of PlaceTile arguments");
        //
        //                var args = start.GetInstructionsUntil(OpCodes.Call);
        //
        //                //Remove the tile reference since we dont care about it
        //                args.RemoveAt(0);
        //
        //                Instruction firstNewCall = null; //For later use
        //                foreach (var arg in args)
        //                {
        //                    var ins = arg.Clone();
        //                    il.InsertBefore(start, ins);
        //
        //                    if (null == firstNewCall) firstNewCall = ins;
        //                }
        //                if (setTile.Previous.Previous.OpCode != OpCodes.Call) throw new InvalidOperationException("Expected only one type value instruction");
        //                il.InsertBefore(start, setTile.Previous.Clone()); //The tile type
        //                il.InsertBefore(start, il.Create(OpCodes.Call, cbkOnTileChange));
        //                il.InsertBefore(start, il.Create(OpCodes.Pop));
        //
        //                //Since we could have potentially changed the target instruction for transferring, we must now update it.
        //                start.ReplaceTransfer(firstNewCall, hardUpdateWorld);
        //
        //                //                hardUpdateWorld.Body.OptimizeMacros();
        //                //                hardUpdateWorld.Body.ComputeOffsets();
        //
        //                //Now handle the result of the hook
        //                break;
        //            }
        //        }

        [OTAPatch(SupportType.Server, "Hooking pressure plate triggers")]
        private void HookCollisionPressurePlate()
        {
            //Step 1: Add the calling object as a parameter to Terraria.Collision.SwitchTiles
            //Setp 2: Inject cancellable IL before the HitSwitch branch
            var switchTiles = Terraria.Collision.Method("SwitchTiles");

            //--STEP 1
            //Add the sender parameter
            ParameterDefinition prmSender;
            switchTiles.Parameters.Add(prmSender = new ParameterDefinition("sender", ParameterAttributes.None, Terraria.Entity)
            {
                HasDefault = true,
                IsOptional = true
            });

            //Update all references to add themselves (currently they are all senders!)
            Terraria.ForEachInstruction((mth, ins) =>
                {
                    if (ins.OpCode == OpCodes.Call && ins.Operand == switchTiles)
                    {
                        var cil = mth.Body.GetILProcessor();
                        cil.InsertBefore(ins, cil.Create(OpCodes.Ldarg_0));
                    }
                });

            //--STEP 2
            //Find where the HitSwitch call is (this is our unique reference)
            foreach (var insHitSwitch in switchTiles.Body.Instructions.Where(x => x.OpCode == OpCodes.Call
                                   && x.Operand is MethodReference
                                   && (x.Operand as MethodReference).Name == "HitSwitch").ToArray())
            {
                //Find the branch where we will append our code to
                var insLdLocS = insHitSwitch.FindPreviousInstructionByOpCode(OpCodes.Brfalse_S);

                //Import and get ready for injection
                var hookCall = Terraria.Import(API.CollisionCallback.Method("OnPressurePlateTriggered"));
                var il = switchTiles.Body.GetILProcessor();

                //Add our call to the statement, and leave the result on the branch to take affect to the continutation of the statement
                //These are in reverse order
                var insSkipTo = insLdLocS.Operand as Instruction;
                il.InsertAfter(insLdLocS, il.Create(OpCodes.Brfalse_S, insSkipTo)); //If our hook cancels, then we must skip the trigger
                il.InsertAfter(insLdLocS, il.Create(OpCodes.Call, hookCall)); //Call our code
                il.InsertAfter(insLdLocS, il.Create(OpCodes.Ldloc_S, insHitSwitch.Previous.Previous.Operand as VariableDefinition)); //Load the Y
                il.InsertAfter(insLdLocS, il.Create(OpCodes.Ldloc_S, insHitSwitch.Previous.Operand as VariableDefinition)); //Load the X
                il.InsertAfter(insLdLocS, il.Create(OpCodes.Ldarg, prmSender)); //Load the sender (remember we added this in Step 1)
            }

            switchTiles.Body.OptimizeMacros();
        }

        [OTAPatch(SupportType.Server, "Hooking MechSpawn")]
        private void HookMechSpawn()
        {
            foreach (var type in new TypeDefinition[]
                {
                    Terraria.NPC,
                    Terraria.Item
                })
            {
                var vanilla = type.Methods.Single(x => x.Name == "MechSpawn");
                var hook = Terraria.Import(API.MainCallback.Method("OnMechSpawn"));

                var insLdLoc1 = vanilla.Body.Instructions.Last(x => x.OpCode == OpCodes.Ldloc_1);
                var insLdcI40 = vanilla.Body.Instructions.Last(x => x.OpCode == OpCodes.Ldc_I4_0);

                var il = vanilla.Body.GetILProcessor();

                //Add all the parameters
                foreach (var prm in vanilla.Parameters)
                    il.InsertBefore(insLdLoc1, il.Create(OpCodes.Ldarg, prm));

                //Add the first three variables
                for (var x = 0; x < 3; x++)
                    il.InsertBefore(insLdLoc1, il.Create(OpCodes.Ldloc, vanilla.Body.Variables[x]));

                if (Terraria.Item == vanilla.DeclaringType)
                    il.InsertBefore(insLdLoc1, il.Create(OpCodes.Ldc_I4_1));
                else if (Terraria.NPC == vanilla.DeclaringType)
                    il.InsertBefore(insLdLoc1, il.Create(OpCodes.Ldc_I4_2));
                else throw new NotSupportedException("Target is not supported");

                il.InsertBefore(insLdLoc1, il.Create(OpCodes.Call, hook));
                il.InsertBefore(insLdLoc1, il.Create(OpCodes.Brfalse_S, insLdcI40));

                vanilla.Body.OptimizeMacros();
            }
        }

#if SAVE_PROGRESS
        [OTAPatch(SupportType.Server, "Hooking world save tiles progress")]
        private void PatchWorldSaveTileProgress()
        {
            var callback = Terraria.Import(API.WorldFileCallback.Method("OnSaveWorldTiles_Progress"));
            var method = Terraria.WorldFile.Method("SaveWorldTiles");

            var il = method.Body.GetILProcessor();
            var st = method.Body.Instructions.Single(x => x.OpCode == OpCodes.Call
                         && x.Operand is MethodReference
                         && (x.Operand as MethodReference).Name == "Concat"
                         && x.Next.OpCode == OpCodes.Stsfld
                         && (x.Next.Operand is FieldReference)
                         && (x.Next.Operand as FieldReference).Name == "statusText");

            st.Operand = callback;
            il.Remove(st.Next);

            var index = method.Body.Variables.First(x => x.VariableType.Name == "Int32");
            il.InsertAfter(st.Previous, il.Create(OpCodes.Ldloc, index));
        }

        [OTAPatch(SupportType.Server, "Hooking world validating progress")]
        private void PatchValidateWorldProgress()
        {
            var callback = Terraria.Import(API.WorldFileCallback.Method("OnValidateWorld_Progress"));
            var method = Terraria.WorldFile.Method("validateWorld");

            var il = method.Body.GetILProcessor();
            var st = method.Body.Instructions.Single(x => x.OpCode == OpCodes.Call
                         && x.Operand is MethodReference
                         && (x.Operand as MethodReference).Name == "Concat"
                         && x.Next.OpCode == OpCodes.Stsfld
                         && (x.Next.Operand is FieldReference)
                         && (x.Next.Operand as FieldReference).Name == "statusText");

            st.Operand = callback;
            il.Remove(st.Next);

            var index = method.Body.Instructions
                .Single(x => x.Operand is FieldReference
                            && (x.Operand as FieldReference).Name == "maxTilesX")
                .Previous.Previous.Operand as VariableDefinition;
            il.InsertAfter(st.Previous, il.Create(OpCodes.Ldloc, index));
        }

        [OTAPatch(SupportType.Server, "Hooking world saving text", 50)]
        private void PatchSaveWorldStatusTexts()
        {
            var callback = Terraria.Import(API.WorldFileCallback.Method("OnSaveWorldDirect_StatusText"));
            var method = Terraria.WorldFile.Methods.Single(x => x.Name == "saveWorld" && x.Parameters.Count == 2);

            var il = method.Body.GetILProcessor();

            var statusTexts = method.Body.Instructions
                .Where(x => x.Operand is FieldReference
                                  && (x.Operand as FieldReference).Name == "statusText");

            foreach (var st in statusTexts.ToArray())
            {
                il.Replace(st, il.Create(OpCodes.Call, callback));
            }
        }
#endif
    }
}
