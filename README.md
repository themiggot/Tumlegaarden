# Tumlegaarden hjemmeside

Tech demo: ASP.NET Core 10 (Razor Pages) med et 16-bit skrivebordstema og et sikkert login, der kun er åbent for `@tumlegaarden.dk`.
Der er **ingen JavaScript** på sitet – alt kører på serveren, og CSP-headeren forbyder scripts helt.

## Funktioner

- **Startside** efter login (`/`, via den gule "Start"-knap i menuen eller ved klik på "Tumlegaarden"): "Tumlegaarden – Det online paradis", officielt game og reglementet. Teksten står i `Pages/Index.cshtml`.
- **Kalender** på `/Calendar`. Begivenheder har startdato og evt. tidspunkt, en valgfri slutdato/-tid (så de kan vare flere dage), navn og brødtekst, ligger i tabellen `Events` og foldes ud ved klik (HTML `<details>`, ingen JavaScript).
- **Deltagelse:** alle kan svare *Deltager* eller *Deltager ikke* på en begivenhed (tabellen `EventResponses`). Samme knap igen fjerner svaret.
- **Albums:** alle kan oprette albums og uploade billeder (JPEG, PNG, WebP, GIF). Billederne kodes om med [SkiaSharp](https://github.com/mono/SkiaSharp): orientering rettes, GPS og andre metadata fjernes, og der gemmes en version på højst 2048 px plus en miniature. Filerne ligger i `/tumlegaarden-data/albums` uden for web-mappen og udleveres kun til brugere, der er logget ind. Maks. størrelse pr. billede (standard 5 MB) styres af administratorer under **Indstillinger** (tabellen `Settings`).
- **Chatbeskeder slettes efter 24 timer** af et baggrundsjob (hvert kvarter og ved opstart). Gamle beskeder vises heller ikke, selv hvis appen har været i dvale, og jobbet ikke har kørt.
- **Disclaimer** ved første besøg: privat side uden kommercielle interesser, kun for inviterede. Skal bekræftes med OK. Indeholder også cookie-informationen.
- **Chat** nederst til højre på alle sider. Rummet "Alle" er for alle brugere. Diktatorer og administratorer opretter rum og vælger medlemmer under **Indstillinger** og kan se alle rum. Alle beskeder slettes automatisk efter 24 timer. Uden JavaScript virker chatten som en almindelig side på `/Chat`.
- **Privatliv (GDPR):** privatlivssiden (`/Cookies`, kan ses uden login) nævner den dataansvarlige (admin@tumlegaarden.dk), formål og retsgrundlag, databehandlere (Simply.com), hvor længe data gemmes, brugernes rettigheder og klage til Datatilsynet. Slettes en bruger, erstattes e-mailen på begivenheder, albums, billeder, chatrum og -beskeder med "slettet bruger", og afsluttede invitationer til adressen slettes (`Services/UserDataEraser.cs`, kører også ved opstart). Logfiler ældre end 30 dage slettes automatisk (`Services/LogCleanupService.cs`).
- **Hvem er online:** "● 3 online" i menuen (navnene vises ved mus-over), en kolonne på **Brugere** med "Online" eller "Sidst set …", og en liste i chatboksen. Online = fuldt logget ind (med to-faktor) og aktiv inden for 2 minutter; chatboksen holder en åben side aktiv. Gemmes kun i hukommelsen og nulstilles ved genstart. Log ud gør en bruger offline med det samme; låste og slettede brugere vises aldrig som online.
- **Ordjagt** (`/Ordjagt`): gæt dagens ord på 5 bogstaver i højst 6 forsøg, én gang om dagen; samme ord for alle, nyt ved midnat dansk tid. Bogstaverne markeres som rigtig plads, findes i ordet eller findes ikke (korrekt med dobbeltbogstaver). Point: 100/80/60/40/25/10 for 1–6 forsøg, 0 ved tab. Highscore med point, korrekte og forkerte spil, bedste og gennemsnit, plus egen statistik (tabellen `GameRounds`). Et påbegyndt spil, der ikke afsluttes inden midnat, tæller som forkert. Al logik kører på serveren; ordet sendes ikke til browseren før spillet er slut, og rækkefølgen af ord bestemmes af en hemmelig nøgle i databasen (repoet er offentligt). Dagens ord vælges blandt 1.564 almindelige ord (`GameData/ord-dagens.txt`: de mest brugte ord på 5 bogstaver ifølge en dansk frekvensliste fra FrequencyWords/OpenSubtitles, CC BY-SA 4.0, renset for navne, engelske ord, bandeord og ejefald, plus håndplukkede ord); hvert spil gemmer sit ord, så gamle spil vises rigtigt, selv om listen ændres. **Øvespil** (`/Ordjagt/Oev`): ubegrænset antal spil med tilfældige ord (aldrig dagens ord), tæller ikke med i highscoren; tilstanden ligger i en krypteret cookie. Gæt godkendes mod 9.437 ord inkl. bøjninger fra Stavekontrolden (`GameData/ord-gyldige.txt`, MPL-1.1, lavet med `tools/expand_hunspell.py`).
- **Spil-menuen:** knappen "Spil" står lige efter "Start" og glimter (slås fra ved "reducer bevægelse"). Den åbner en dropdown (`<details>`, virker uden JavaScript; `forms.ts` lukker den ved klik udenfor og Esc) med alle spil, så der er plads til flere.
- **Ordjagt After Dark** (`/AfterDark`, øvespil på `/AfterDark/Oev`, 18+): samme spil med bandeord, frække ord og natteliv som dagens ord (`GameData/ord-afterdark.txt`, håndplukket; skældsord mod grupper er udeladt med vilje). Alle almindelige ord kan bruges som gæt. Eget dagens ord (nøglen `Game.AfterDark.Secret`), egen highscore (tabellen `AfterDarkRounds`, oprettes automatisk), egen øvespil-cookie og mørkt neon-tema. Varianterne deler koden (`Services/GameModes.cs`, `Pages/Game/DailyGameModel.cs`, `_Daily`/`_Practice`).
- **Indstillinger** samler alle indstillinger: billedstørrelse for albums (kun administratorer) og chatrum (diktatorer og administratorer).
- **Kun adgang fra Danmark** (IP-filter på alle sider). Se [DEPLOY.md](DEPLOY.md#8a-kun-adgang-fra-danmark).
- **Login** kun for `@tumlegaarden.dk`, kun via invitation, obligatorisk to-faktor.

## Brugerniveauer

| Niveau | Rettigheder |
|---|---|
| **Administrator** | Alt. Eneste der kan invitere nye brugere og ændre billedstørrelsen under **Indstillinger**. Kan redigere alle andre brugere og tildele alle niveauer. |
| **Diktator** | Kan oprette chatrum og vælge medlemmer, og se alle rum. Kan redigere børlere (niveau, lås, nulstil to-faktor, slet) og gøre dem til diktator. Kan rette og slette alle begivenheder, albums og billeder. |
| **Børler** | Kan oprette begivenheder og albums, uploade billeder og rette/slette sit eget indhold. |

Alle kan se kalenderen, albums og brugerlisten og svare på begivenheder. Ingen kan redigere sig selv på brugersiden.

Man kan **ikke** oprette sig selv. En konto oprettes kun via et engangs-invitationslink, som en administrator sender fra siden *Brugere*.
Linket udløber efter 72 timer, kan kun bruges én gang, og kun en SHA-256-hash af det gemmes i databasen.
En administrator kan ikke ændre eller slette sig selv, så der altid er mindst én administrator.
Ændringer af niveau, låsning og sletning slår igennem på aktive sessioner inden for et minut.

**Første administrator:** Når databasen er tom ved opstart, sendes en administrator-invitation til adressen i `Bootstrap:AdminEmail`.

## Cookies (GDPR)

Sitet bruger kun nødvendige cookies (login, to-faktor, CSRF-beskyttelse, statusbeskeder og husk-meddelelsen). Den slags kræver ikke samtykke efter cookiebekendtgørelsen,
men brugeren skal informeres. Derfor vises en cookie-meddelelse nederst med link til siden `/Cookies`, som beskriver hver cookie og den persondata der gemmes.
Tilføjer I senere statistik eller andet, der ikke er nødvendigt, skal meddelelsen udvides med et rigtigt til-/fravalg **før** cookies sættes.

## Sikkerhed

| Område | Hvad sitet gør |
|---|---|
| Domæne | Kun `@tumlegaarden.dk` kan oprette konto og logge ind (tjekkes i formularen **og** i en `IUserValidator`, så det også gælder i databasen). Eksakt domæne – ingen subdomæner eller `tumlegaarden.dk.evil.com`. |
| Oprettelse | Kun via engangs-invitation fra en administrator. Linket sendes til adressen og beviser dermed ejerskab af mailen. |
| Adgangskode | Mindst 14 tegn, store og små bogstaver, tal, specialtegn, mindst 6 forskellige tegn. Må ikke indeholde e-mailen, "tumlegaarden" eller almindelige ord/mønstre, og afvises hvis den findes i kendte datalæk (Have I Been Pwned, k-anonymity – adgangskoden forlader aldrig serveren). |
| Hashing | PBKDF2-HMAC-SHA512 med 210.000 iterationer (OWASP-anbefaling). |
| To-faktor-hemmeligheder | Authenticator-nøglen krypteres med ASP.NET Data Protection (nøgler i `/tumlegaarden-data/keys`, ikke i databasen). Gendannelseskoderne hashes med PBKDF2-SHA256 (20.000 iterationer, salt pr. kode). Et læk af databasen alene afslører hverken adgangskoder, nøgler eller koder. Ældre værdier i klartekst konverteres automatisk ved opstart. |
| To-faktor | **Obligatorisk** TOTP (authenticator-app). Uden 2FA kan man kun se opsætningssiden. 10 engangs-gendannelseskoder. |
| Brute force | Kontoen låses 15 min efter 5 forkerte forsøg. Maks. 30 login-/oprettelsesforsøg pr. minut pr. IP. |
| Kontoopslag | Login og "glemt kode" svarer ens uanset om kontoen findes. |
| Sessioner | `__Host-`-cookies: `Secure`, `HttpOnly`, `SameSite=Strict`. 30 min. inaktivitet logger ud. Skift af adgangskode logger alle andre sessioner ud. |
| Headers | Streng CSP (ingen scripts), HSTS, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer`, `no-store` caching, serverheader fjernet. |
| Øvrigt | CSRF-tokens på alle formularer, ingen open redirects, ingen Google Fonts (pixelfonten ligger lokalt – GDPR). |

### SSO via Simply.com
Simply.com tilbyder **ikke** en SSO-/OpenID Connect-tjeneste, som et website kan logge ind imod – deres mailkonti kan kun bruges til selve mailen.
Derfor har sitet sine egne konti, beskyttet som beskrevet ovenfor. Hvis Tumlegaarden senere får Microsoft 365 eller Google Workspace,
kan der tilføjes rigtig SSO (OpenID Connect) med ganske få linjer i `Program.cs`, begrænset til tumlegaarden.dk-tenanten.

## TypeScript

Sitet bruger kun lidt JavaScript, og alt virker også uden:
- `Scripts/chat.ts`: chatboksen nederst til højre.
- `Scripts/game.ts`: skærmtastaturet i Ordjagt og visning af bogstaverne, mens man skriver.
- `Scripts/forms.ts`: et klik hvor som helst i et dato- eller tidsfelt åbner browserens kalender eller ur, og slutdatoen kan ikke vælges før startdatoen.

Det byggede JavaScript i `wwwroot/js` er committet, så `dotnet build` ikke kræver Node.

```bash
npm install
npm run build:ts
```

GitHub Actions stopper bygget, hvis filerne i `wwwroot/js` ikke passer til `Scripts/*.ts`. Chatboksen henter nye beskeder hvert 4. sekund, når den er åben, og ellers hvert 30. sekund for at vise markeringen for nye beskeder. Al tekst indsættes med `textContent`, og CSP tillader kun scripts fra sitet selv.

## Tests

```bash
dotnet test tests/Tumlegaarden.Tests
```

Tester landefilteret (danske og udenlandske adresser, undtagelser, manglende data), domænekontrollen af e-mails, rangreglerne for redigering af brugere, online-status, krypteringen af to-faktor-hemmeligheder (inkl. konvertering af gamle værdier), Ordjagt (vurdering af gæt med dobbeltbogstaver, point, ordlister og at dagens ord ikke gentages; After Dark-listen, separate nøgler, cookies og tabeller) og billedbehandlingen (alle 8 EXIF-orienteringer, fjernelse af metadata, formater og afvisning af ikke-billeder; testbillederne ligger i `tests/Tumlegaarden.Tests/Fixtures`). Kører også ved hvert byg på GitHub.

## Tredjepartskomponenter

- **SkiaSharp** (MIT): billedbehandling i albums. Erstattede ImageSharp, hvis version 3.1 har kendte sikkerhedshuller, og hvis rettede versioner kræver en licensnøgle.
- **MailKit**, **QRCoder**, **MySql.EntityFrameworkCore**: MIT/GPL-kompatible open source-licenser.
- **Skrifttyper:** DotGothic16 og Silkscreen (SIL Open Font License), ligger i `wwwroot/fonts`.
- **IP-data:** NRO via ip-location-db (CC BY 4.0), se `GeoData/LICENSE-NRO.txt`.
- **Ordlister til Ordjagt:** Stavekontrolden da_DK 2.8 (LGPL-2.1/GPL-2/MPL-1.1, brugt under MPL-1.1) og ordfrekvenser fra FrequencyWords af Hermit Dave (OpenSubtitles, CC BY-SA 4.0), se `GameData/LICENSE-Stavekontrolden.txt`.

## Kør lokalt

```bash
cd src/Tumlegaarden.Web
dotnet run --launch-profile https
```

Åbn https://localhost:7195. Lokalt sendes der ingen mails – links skrives i konsollen. Ved første start skrives en invitation til `admin@tumlegaarden.dk` i konsollen; åbn den for at oprette den første administrator.
Databasen er SQLite i `App_Data/` og oprettes automatisk.

### Datamappe
Database-fil, krypteringsnøgler og `appsettings.Production.json` ligger i datamappen: `App:DataPath` hvis sat, ellers mappen
`tumlegaarden-data` ved siden af appens mappe, hvis den findes, ellers `App_Data/` i appen. Den valgte mappe skrives i loggen ved opstart.

### Database
Sitet vælger database ud fra `ConnectionStrings` i denne rækkefølge: `MySql` (bruges hos Simply.com), `SqlServer`, og ellers en SQLite-fil i `App_Data/`.
MySQL-driveren er Oracles `MySql.EntityFrameworkCore`, fordi Pomelo endnu ikke understøtter .NET 10. Testet mod MySQL 8.0.
Får I en SSL-fejl ved forbindelsen, så skift `SslMode=Required` til `SslMode=Preferred`.

## Udgiv til Simply.com

Se **[DEPLOY.md](DEPLOY.md)** for den komplette vejledning.

Kort fortalt: GitHub Actions bygger sitet ved hver push til `main` og lægger det færdige site på grenen `release`, som er repoets standard-gren.
Simply.com's **Importer fra Git** henter `release` ind i `/public_html`. Hemmeligheder og data ligger i `/tumlegaarden-data` ved siden af,
så en import aldrig rører dem. IIS kører appen med shadow copy, så importen kan overskrive filerne, mens sitet kører.

