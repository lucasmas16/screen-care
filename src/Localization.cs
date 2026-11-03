using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace ScreenCare
{
    public enum Language { English, Portuguese }

    internal static class L10n
    {
        internal static Language Detect(CultureInfo culture)
        { return culture.TwoLetterISOLanguageName == "pt" ? Language.Portuguese : Language.English; }
        internal static Language Current = Detect(CultureInfo.CurrentUICulture);
        internal static string T(string portuguese) { return Get(portuguese, Current); }
        internal static string Get(string portuguese, Language language)
        {
            if (String.IsNullOrEmpty(portuguese) || language == Language.Portuguese) return portuguese;
            string english;
            if (English.TryGetValue(portuguese, out english)) return english;
            foreach (string prefix in new[] { "Diminuir: ", "Aumentar: " })
                if (portuguese.StartsWith(prefix, StringComparison.Ordinal))
                    return English[prefix] + Get(portuguese.Substring(prefix.Length), language);
            return portuguese;
        }
        internal static string Format(string portuguese, params object[] values)
        { return String.Format(CultureInfo.InvariantCulture, T(portuguese), values); }
        internal static string Duration(int value, int unit, Language language)
        {
            string name = unit == 2 ? (value == 1 ? "hora" : "horas") : unit == 1 ? (value == 1 ? "minuto" : "minutos") : (value == 1 ? "segundo" : "segundos");
            return value.ToString(CultureInfo.InvariantCulture) + " " + Get(name, language);
        }
        internal static ComboBox LanguagePicker(Language language)
        {
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Idioma", Width = 166 };
            picker.Items.AddRange(new object[] { "English", "Português (Brasil)" });
            picker.SelectedIndex = (int)language;
            return picker;
        }

        // The Portuguese source text is also the catalog key. User-entered values
        // and messages supplied by Windows are never passed through this table.
        internal static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Idioma", "Language" },
            { "Desligar telas em 2 segundos", "Turn off displays in 2 seconds" },
            { "As telas serão desligadas em 2 segundos.", "Displays will turn off in 2 seconds." },
            { "O atalho Ctrl+Alt+F11 está em uso. Use o menu para desligar as telas.", "Ctrl+Alt+F11 is already in use. Use the menu to turn off displays." },
            { "A automação foi pausada após um erro. O app continua aberto. Revise as configurações e ative novamente.", "Automation was paused after an error. The app is still running. Review settings and enable it again." },
            { "Não foi possível monitorar teclado e mouse.", "Could not monitor keyboard and mouse input." },
            { "Não foi possível acompanhar a sessão e os monitores.", "Could not monitor the session and displays." },
            { "Valor", "Value" }, { "Diminuir", "Decrease" }, { "Aumentar", "Increase" },
            { "Um descanso para suas telas.", "Give your screens a rest." },
            { "SUAS TELAS", "YOUR SCREENS" },
            { "Seu álbum, em pausa.", "Your photos, on a break." },
            { "Hora de descansar.", "Time to rest." },
            { "No seu controle.", "You are in control." },
            { "A ação automática está pausada.", "Automatic action is paused." },
            { "Suas imagens em todas as telas, após {0} sem atividade.", "Your photos on every screen after {0} of inactivity." },
            { "Todos os monitores desligam após {0} sem atividade.", "All monitors turn off after {0} of inactivity." },
            { "Volte com um toque no teclado\nou um movimento do mouse.", "Return with a key press\nor a mouse movement." },
            { "o tempo escolhido", "the selected time" },
            { "hora", "hour" }, { "horas", "hours" },
            { "minuto", "minute" }, { "minutos", "minutes" },
            { "segundo", "second" }, { "segundos", "seconds" },
            { "seg", "sec" }, { "Segundos", "Seconds" }, { "Minutos", "Minutes" }, { "Horas", "Hours" },
            { "Ativar automação", "Enable automation" },
            { "Automação", "Automation" },
            { "Ativa após um período sem interação", "Starts after a period of inactivity" },
            { "Desligar telas", "Turn off displays" },
            { "Economizar energia", "Save energy" },
            { "Exibir suas imagens", "Show your photos" },
            { "Modo desligar monitores", "Turn off monitors mode" },
            { "Modo slideshow", "Slideshow mode" },
            { "Tempo de espera", "Idle timeout" },
            { "Pasta de imagens", "Image folder" },
            { "Escolher…", "Browse…" },
            { "Escolher pasta de imagens", "Choose image folder" },
            { "Escolha a pasta com as imagens do slideshow", "Choose the folder containing your slideshow images" },
            { "Com o Windows bloqueado, os monitores serão desligados.", "When Windows is locked, monitors will turn off." },
            { "Trocar a imagem a cada", "Change image every" },
            { "Intervalo entre imagens", "Slide interval" },
            { "Incluir subpastas", "Include subfolders" },
            { "Subpastas", "Subfolders" },
            { "Ordem aleatória", "Shuffle" },
            { "Só a sua atividade conta.", "Only your activity counts." },
            { "Teclado, mouse e touchpad reiniciam a espera.\n\nVídeos e músicas continuam tocando enquanto as telas descansam.", "Keyboard, mouse and touchpad activity reset the timer.\n\nVideos and music keep playing while your screens rest." },
            { "Iniciar com o Windows", "Start with Windows" },
            { "Retomar as últimas configurações", "Restore your last settings" },
            { "Fechar mantém o app na bandeja.\nCtrl+Alt+F11: desligar telas em 2 segundos.", "Closing keeps the app in the tray.\nCtrl+Alt+F11: turn off displays in 2 seconds." },
            { "Cancelar", "Cancel" }, { "Salvar configurações", "Save settings" },
            { "Não foi possível salvar o tema. ", "Could not save the theme. " },
            { "Não foi possível salvar o idioma. ", "Could not save the language. " },
            { "Informe um tempo de espera válido.", "Enter a valid idle timeout." },
            { "Escolha um tempo entre 10 segundos e 24 horas.", "Choose a timeout between 10 seconds and 24 hours." },
            { "Escolha um intervalo entre 2 e 3600 segundos para as imagens.", "Choose a slide interval between 2 and 3600 seconds." },
            { "Escolha uma pasta de imagens disponível.", "Choose an available image folder." },
            { "Tema escuro", "Dark theme" },
            { "Ativado: escuro. Desativado: claro. Espaço alterna o tema.", "On: dark. Off: light. Press Space to switch themes." },
            { "Claro", "Light" }, { "Escuro", "Dark" },
            { "Diminuir: ", "Decrease: " }, { "Aumentar: ", "Increase: " },
            { "Configurações…", "Settings…" },
            { "Sair", "Exit" },
            { "O atalho global Ctrl+Alt+F12 não pôde ser registrado. Ele pode estar em uso por outro aplicativo.", "The Ctrl+Alt+F12 global shortcut could not be registered. Another app may be using it." },
            { "O Windows não aceitou o pedido para desligar os monitores.", "Windows did not accept the request to turn off the monitors." },
            { "Não foi possível salvar. ", "Could not save. " },
            { "Monitoramento automático ativado.", "Automatic monitoring enabled." },
            { "Monitoramento automático desativado.", "Automatic monitoring disabled." },
            { "Pausar ação automática", "Pause automatic action" },
            { "Ativar ação automática", "Enable automatic action" },
            { "Desligar monitores", "Turn off monitors" }, { "Pausado", "Paused" },
            { "Ativado", "Enabled" }, { "Desativado", "Disabled" }, { "Aviso", "Notice" },
            { "Ctrl+Alt+F12 para alternar", "Ctrl+Alt+F12 to toggle" },
            { "Screen Care — apresentação", "Screen Care — slideshow" },
            { "A pasta não contém imagens compatíveis. Escolha JPG, PNG, BMP, GIF ou TIFF.", "The folder has no supported images. Choose JPG, PNG, BMP, GIF or TIFF." },
            { "Não foi possível abrir a pasta. ", "Could not open the folder. " },
            { "Nenhuma imagem pôde ser aberta. Verifique os arquivos da pasta.", "No images could be opened. Check the files in the folder." },
            { "Não foi possível exibir as imagens. ", "Could not display the images. " },
            { "A pasta de imagens não está disponível.", "The image folder is not available." },
            { "Imagem maior que 64 MB.", "Image exceeds 64 MB." },
            { "Imagem acima de 40 megapixels.", "Image exceeds 40 megapixels." },
            { "Configuração vazia.", "Empty settings." },
            { "Não foi possível ler as configurações salvas. Revise as opções e salve novamente.", "Could not read saved settings. Review your options and save them again." },
            { "O Screen Care precisou encerrar.\n\n", "Screen Care had to close.\n\n" },
            { "O destino contém um link de arquivo ou pasta. Escolha uma pasta local sem links.", "The destination contains a file or folder link. Choose a local folder without links." },
            { "O desinstalador anterior ainda está aberto.", "The previous uninstaller is still open." },
            { "Screen Care desinstalado. Suas configurações foram preservadas.", "Screen Care was uninstalled. Your settings have been preserved." },
            { "O instalador já está aberto.", "The installer is already open." },
            { "Não foi possível concluir.\n\n", "Could not finish.\n\n" },
            { "A pasta de destino já existe e não pertence a esta instalação: ", "The destination folder already exists and does not belong to this installation: " },
            { "Feche o Screen Care pelo ícone perto do relógio e tente novamente.", "Close Screen Care using the tray icon and try again." },
            { "Arquivo ausente no instalador: ", "Missing installer file: " },
            { "Use uma pasta de teste vazia.", "Use an empty test folder." },
            { "Identidade inválida.", "Invalid identity." },
            { "Desinstalar o Screen Care deste usuário?\n\nAs configurações e suas imagens serão preservadas.", "Uninstall Screen Care for this user?\n\nYour settings and images will be preserved." },
            { "Desinstalar Screen Care", "Uninstall Screen Care" },
            { "Esta instalação não foi localizada.", "This installation could not be found." },
            { "Instalar Screen Care", "Install Screen Care" },
            { "Uma pausa para suas telas.", "A break for your screens." },
            { "Desligue os monitores ou exiba suas imagens quando\nvocê parar de usar o computador.\n\nInstalação só para você, sem senha de administrador.", "Turn off monitors or show your photos when\nyou stop using your computer.\n\nInstalls for you only. No administrator password needed." },
            { "Destino: ", "Destination: " },
            { "Instalar e abrir", "Install and open" },
            { "Instalando…", "Installing…" },
            { "Não foi possível instalar", "Could not install" }
        };
    }

    // Retain original labels so switching back never attempts to translate a
    // translation. Text boxes retain their exact user-entered contents.
    internal sealed class LocalizedControls
    {
        private readonly List<Action<Language>> bindings = new List<Action<Language>>();
        internal LocalizedControls(Control root) { Capture(root); }
        private void Capture(Control control)
        {
            string text = control.Text, name = control.AccessibleName, description = control.AccessibleDescription;
            if (!(control is TextBoxBase) && L10n.English.ContainsKey(text)) bindings.Add(delegate(Language l) { control.Text = L10n.Get(text, l); });
            if (!String.IsNullOrEmpty(name)) bindings.Add(delegate(Language l) { control.AccessibleName = L10n.Get(name, l); });
            if (!String.IsNullOrEmpty(description)) bindings.Add(delegate(Language l) { control.AccessibleDescription = L10n.Get(description, l); });
            var tile = control as ModeTile;
            if (tile != null) { string caption = tile.Caption; bindings.Add(delegate(Language l) { tile.Caption = L10n.Get(caption, l); }); }
            foreach (Control child in control.Controls) Capture(child);
        }
        internal void Apply(Language language) { foreach (var binding in bindings) binding(language); }
    }
}
