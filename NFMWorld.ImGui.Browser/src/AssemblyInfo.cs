// Deliberately no [assembly: DisableRuntimeMarshalling]. The generated binding declares a few
// parameters as managed delegates (ImGuiSizeCallback, the Combo/ListBox getters), and with runtime
// marshalling disabled those P/Invokes fail at the call with "Cannot marshal managed types".
