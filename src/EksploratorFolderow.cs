using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapaDyskow {
public static class EksploratorFolderow {
    public static List<Wiersz> Odczytaj(Baza baza,string sciezka){
        var indeks=baza.Zapytaj("SELECT "+Program.KolumnyFolderu+" FROM widok_folderow WHERE rodzic=?",sciezka).Select(Program.Folder).ToDictionary(w=>w.Sciezka,StringComparer.OrdinalIgnoreCase);
        foreach(var plik in baza.Zapytaj("SELECT sciezka,rozmiar,zmiana,typ,sha256 FROM pliki WHERE folder=?",sciezka).Select(Program.Plik))indeks[plik.Sciezka]=plik;
        var wynik=new List<Wiersz>();
        // Wyłącznie bezpośrednie elementy. Indeks nigdy nie decyduje o ich widoczności.
        foreach(var wpis in new DirectoryInfo(sciezka).EnumerateFileSystemInfos()){
            bool folder=wpis is DirectoryInfo;Wiersz element;
            if(!indeks.TryGetValue(wpis.FullName,out element)||element.Rodzaj!=(folder?"folder":"plik"))element=new Wiersz {Rodzaj=folder?"folder":"plik",Rozmiar=folder?-1:0,Pliki=-1,Podfoldery=-1,Oznaczenia="Nieprzeanalizowany"};
            element.Sciezka=wpis.FullName;element.Nazwa=wpis.Name;
            try{element.Utworzenie=wpis.CreationTime;element.Dostep=wpis.LastAccessTime;element.Atrybuty=wpis.Attributes;double zmiana=(wpis.LastWriteTimeUtc-Program.Epoka).TotalSeconds;
                if(!folder){long rozmiar=((FileInfo)wpis).Length;if(element.Rozmiar!=rozmiar||Math.Abs(element.Zmiana-zmiana)>0.000001){element.Hash=null;element.WIndeksie=false;element.Oznaczenia="Nieprzeanalizowany";}element.Rozmiar=rozmiar;}element.Zmiana=zmiana;
            }catch(IOException blad){element.BladOdczytu=blad.Message;}catch(UnauthorizedAccessException blad){element.BladOdczytu=blad.Message;}
            wynik.Add(element);
        }
        return wynik;
    }
}
}
