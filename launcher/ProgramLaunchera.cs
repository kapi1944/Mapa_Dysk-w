using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LauncherMapy {
public static class ProgramLaunchera {
    [STAThread] public static int Main(string[] argumenty){
        string repo=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
        try{
            if(argumenty.Length==1&&argumenty[0]=="--testy")return TestyLaunchera.Uruchom(repo);
            var wydanie=new WydanieLokalne(repo);var ustawienia=wydanie.WczytajUstawienia();
            if(argumenty.Length==1&&argumenty[0]=="--przygotuj"){var wynik=wydanie.Przygotuj(ustawienia.AutomatycznieBuduj,s=>wydanie.ZapiszLog(s)).GetAwaiter().GetResult();return wynik.Blad==null?0:1;}
            var aplikacja=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var okno=new OknoLaunchera(aplikacja,wydanie,ustawienia);
            aplikacja.Dispatcher.BeginInvoke(new Action(()=>{if(argumenty.Length==1&&argumenty[0]=="--ustawienia")okno.PokazUstawienia();else okno.Uruchom();}));
            aplikacja.Run();return 0;
        }catch(Exception blad){MessageBox.Show(blad.Message,"Mapa dysków — launcher",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
    }
}
sealed class OknoLaunchera {
    readonly Application aplikacja;readonly WydanieLokalne wydanie;readonly UstawieniaLaunchera ustawienia;
    readonly Window okno;readonly TextBlock opis;readonly ProgressBar postep;readonly StackPanel przyciski;
    bool trwa;
    public OknoLaunchera(Application aplikacja,WydanieLokalne wydanie,UstawieniaLaunchera ustawienia){
        this.aplikacja=aplikacja;this.wydanie=wydanie;this.ustawienia=ustawienia;
        okno=new Window {Title="Mapa dysków",Width=580,Height=275,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Brush("#141B26"),Foreground=Brushes.White,FontFamily=new FontFamily("Segoe UI")};
        var panel=new StackPanel {Margin=new Thickness(24)};okno.Content=panel;
        panel.Children.Add(new TextBlock {Text="MAPA DYSKÓW",FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        opis=new TextBlock {Text="Przygotowanie aplikacji…",TextWrapping=TextWrapping.Wrap,FontSize=14,Foreground=Brush("#CBD5E6"),Margin=new Thickness(0,0,0,16)};panel.Children.Add(opis);
        postep=new ProgressBar {IsIndeterminate=true,Height=5,Margin=new Thickness(0,0,0,16)};panel.Children.Add(postep);
        var automatycznie=new CheckBox {Content="Automatycznie buduj po zmianach źródeł",IsChecked=ustawienia.AutomatycznieBuduj,Foreground=Brushes.White,Margin=new Thickness(0,0,0,16)};
        automatycznie.Click+=(s,e)=>{try{ustawienia.AutomatycznieBuduj=automatycznie.IsChecked==true;wydanie.ZapiszUstawienia(ustawienia);}catch(Exception blad){wydanie.ZapiszLog(blad.ToString());MessageBox.Show(okno,blad.Message,"Ustawienia launchera");}};panel.Children.Add(automatycznie);
        przyciski=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(przyciski);
        okno.Closing+=(s,e)=>{if(trwa)e.Cancel=true;else aplikacja.Shutdown();};
    }
    static Brush Brush(string kolor){return (Brush)new BrushConverter().ConvertFromString(kolor);}
    void Przycisk(string tekst,Action akcja,bool dostepny=true){var przycisk=new Button {Content=tekst,IsEnabled=dostepny,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,8,0),Background=Brush("#293D58"),Foreground=Brushes.White,BorderBrush=Brush("#486383")};przycisk.Click+=(s,e)=>akcja();przyciski.Children.Add(przycisk);}
    public void PokazUstawienia(){postep.Visibility=Visibility.Collapsed;opis.Text="Ustawienie jest zapisywane lokalnie. Pełne testy uruchamiaj osobno przez TEST-LOCAL.ps1.";Przycisk("Uruchom aplikację",Uruchom);Przycisk("Zamknij",()=>okno.Close());okno.Show();}
    public async void Uruchom(){
        trwa=true;przyciski.Children.Clear();postep.Visibility=Visibility.Visible;
        try{var wynik=await wydanie.Przygotuj(ustawienia.AutomatycznieBuduj,tekst=>aplikacja.Dispatcher.BeginInvoke(new Action(()=>{opis.Text=tekst;if(!okno.IsVisible)okno.Show();})));
            trwa=false;if(wynik.Blad!=null){PokazBlad(wynik.Blad);return;}
            wydanie.UruchomAplikacje();aplikacja.Shutdown();
        }catch(Exception blad){trwa=false;wydanie.ZapiszLog(blad.ToString());PokazBlad(blad.Message);}
    }
    void PokazBlad(string blad){
        opis.Text="Nie udało się zbudować najnowszej wersji";opis.ToolTip=blad;postep.Visibility=Visibility.Collapsed;przyciski.Children.Clear();
        Przycisk("Uruchom ostatnią działającą",()=>{try{wydanie.UruchomAplikacje();aplikacja.Shutdown();}catch(Exception problem){MessageBox.Show(okno,problem.Message,"Ostatnia działająca wersja");}},wydanie.PoprawneWydanie());
        Przycisk("Pokaż log",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","notepad.exe"),"\""+wydanie.PlikLog+"\""){UseShellExecute=true}));
        Przycisk("Zamknij",()=>okno.Close());if(!okno.IsVisible)okno.Show();
    }
}
}
