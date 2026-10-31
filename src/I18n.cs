using System;
using System.Collections.Generic;

namespace TecnoSpace3D
{
    public sealed class ActionItem
    {
        public string Key { get; private set; }
        public string Label { get; private set; }

        public ActionItem(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    public static class I18n
    {
        public static bool IsEn(string lang)
        {
            return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        }

        public static string T(string lang, string key)
        {
            bool en = IsEn(lang);
            switch (key)
            {
                case "WinTitle":
                    return en
                        ? "TecnoSpace3D Hub - SpaceBall 5000 Multi-CAD (SW / Inventor / Solid Edge / AutoCAD / Blender)"
                        : "TecnoSpace3D Hub - SpaceBall 5000 Multi-CAD (SW / Inventor / Solid Edge / AutoCAD / Blender)";
                case "HeaderTitle":
                    return en
                        ? "TECNOSPACE 3D - MULTI-CAD 6DoF CONTROLLER"
                        : "TECNOSPACE 3D - CONTROLADOR 6DoF MULTI-CAD";
                case "BtnZero":
                    return en ? "Zero Sensor (Drift)" : "Tarar Zero (Drift)";
                case "UsbPrefix":
                    return "USB HID: ";
                case "CadPrefix":
                    return en ? "Active CAD: " : "CAD Ativo: ";
                case "SecAxes":
                    return en ? "LIVE 6-AXIS TELEMETRY + INVERSION" : "TELEMETRIA 6 EIXOS AO VIVO + INVERSAO";
                case "AxisTx":
                    return en ? "Pan X (Left/Right)" : "Pan X (Esq/Dir)";
                case "AxisTy":
                    return en ? "Pan Y (Up/Down)" : "Pan Y (Cima/Baixo)";
                case "AxisTz":
                    return en ? "Zoom Z (Fwd/Back)" : "Zoom Z (Frente/Tras)";
                case "AxisRx":
                    return en ? "Pitch RX (Tilt)" : "Pitch RX (Inclinar)";
                case "AxisRy":
                    return en ? "Yaw RY (Spin)" : "Yaw RY (Girar)";
                case "AxisRz":
                    return en ? "Roll RZ (Roll)" : "Roll RZ (Tombar)";
                case "Inv":
                    return "Inv";
                case "SecSens":
                    return en ? "SENSITIVITY & RESPONSE CURVE" : "SENSIBILIDADE E CURVA DE RESPOSTA";
                case "SlMaster":
                    return en ? "Master Speed" : "Velocidade Geral";
                case "SlPan":
                    return en ? "Pan Speed" : "Velocidade Pan";
                case "SlZoom":
                    return en ? "Zoom Speed" : "Velocidade Zoom";
                case "SlRot":
                    return en ? "Rotation Speed" : "Velocidade Rotacao";
                case "SlDz":
                    return en ? "Deadzone" : "Zona Morta (Deadzone)";
                case "ChkRot":
                    return en ? "3D Rotation Enabled" : "Rotacao 3D Ativa";
                case "ChkDom":
                    return en ? "Dominant Axis (1-axis)" : "Modo Dominante (1 eixo)";
                case "ChkSwap":
                    return en ? "Swap Y <-> Z Axes" : "Trocar Eixos Y <-> Z";
                case "ChkAuto":
                    return en ? "Start with Windows" : "Iniciar com o Windows";
                case "SecBtns":
                    return en ? "12-BUTTON MAPPING (SPACEBALL 5000)" : "MAPEAMENTO DOS 12 BOTOES (SPACEBALL 5000)";
                case "HintPressBtn":
                    return en
                        ? "Press any button (1..9, A, B, C) on SpaceBall to test"
                        : "Pressione qualquer botao (1..9, A, B, C) no SpaceBall para testar";
                case "PressedPrefix":
                    return en ? "Button pressed: " : "Botao pressionado: ";
                case "BtnWord":
                    return en ? "Button " : "Botao ";
                case "TrayOpen":
                    return en ? "Open TecnoSpace3D Dashboard" : "Abrir Painel TecnoSpace3D";
                case "TrayZero":
                    return en ? "Zero Sensor (Drift)" : "Tarar Zero (Sensor)";
                case "TrayLang":
                    return en ? "Language: Português (PT-BR)" : "Idioma: English (EN-US)";
                case "TrayQuit":
                    return en ? "Quit TecnoSpace3D" : "Encerrar TecnoSpace3D";
                case "BalloonTitle":
                    return en ? "TecnoSpace3D Running" : "TecnoSpace3D Ativo";
                case "BalloonBody":
                    return en
                        ? "Running in background for all your CAD suites."
                        : "Rodando em segundo plano para todos os seus CADs.";
                default:
                    return key;
            }
        }

        public static string TranslateAction(string lang, string actionKey)
        {
            bool en = IsEn(lang);
            switch (actionKey)
            {
                case "ZoomToFit": return en ? "Zoom to Fit (F)" : "Enquadrar Vista (Zoom Fit)";
                case "Isometric": return en ? "Isometric View" : "Vista Isometrica";
                case "NormalTo": return en ? "Normal To Face" : "Normal a Face (Normal To)";
                case "Front": return en ? "Front View" : "Vista Frontal";
                case "Top": return en ? "Top View" : "Vista Superior";
                case "Right": return en ? "Right View" : "Vista Direita";
                case "Back": return en ? "Back View" : "Vista Posterior";
                case "Left": return en ? "Left View" : "Vista Esquerda";
                case "Bottom": return en ? "Bottom View" : "Vista Inferior";
                case "Measure": return en ? "Measure Tool" : "Ferramenta Medir (Trena)";
                case "Undo": return en ? "Undo (Ctrl+Z)" : "Desfazer (Ctrl+Z)";
                case "Rebuild": return en ? "Rebuild / Regen" : "Reconstruir (Ctrl+B)";
                case "Escape": return en ? "Escape (ESC)" : "Cancelar (ESC)";
                case "ToggleDominant": return en ? "Toggle Dominant Axis" : "Alternar Modo Dominante";
                case "ToggleRotation": return en ? "Lock / Unlock 3D Rotation" : "Travar / Liberar Rotacao 3D";
                case "ZeroSensor": return en ? "Zero Sensor (Calibrate)" : "Tarar Zero do Sensor";
                case "SpeedUp": return en ? "Increase Master Speed (+)" : "Aumentar Velocidade (+)";
                case "SpeedDown": return en ? "Decrease Master Speed (-)" : "Reduzir Velocidade (-)";
                case "None": return en ? "Disabled (None)" : "Desativado (Nenhum)";
                default: return actionKey;
            }
        }

        public static string TranslateRuntimeStatus(string lang, string rawStatus)
        {
            if (string.IsNullOrEmpty(rawStatus) || !IsEn(lang)) return rawStatus;
            return rawStatus
                .Replace("Aguardando USB (SpaceBall 5000)...", "Waiting for USB (SpaceBall 5000)...")
                .Replace("Erro ao abrir porta HID", "Error opening HID port")
                .Replace("Desconectado", "Disconnected")
                .Replace("Abra SolidWorks, Inventor, Solid Edge, AutoCAD, FreeCAD ou Blender", "Open SolidWorks, Inventor, Solid Edge, AutoCAD, FreeCAD or Blender")
                .Replace("Aguardando abrir CAD...", "Waiting for CAD...")
                .Replace("(Ativo)", "(Active)")
                .Replace("(Janela fechada)", "(Window closed)")
                .Replace("(Fechado)", "(Closed)")
                .Replace("HID Nativo Direto Ativo", "Direct Native HID Active")
                .Replace("Stream UDP 120Hz Ativo", "120Hz UDP Stream Active");
        }
    }
}
