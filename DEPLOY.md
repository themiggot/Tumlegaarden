# Udgivelse til Simply.com

Komplet vejledning til at lægge Tumlegaarden-sitet på webhotellet hos Simply.com og holde det opdateret.
Følg trinene i rækkefølge første gang. Afsnit 8 beskriver opdateringer bagefter.

## Sådan hænger det sammen

```
GitHub: main  ──(GitHub Actions bygger)──►  GitHub: release  ──(Simply "Importer fra Git")──►  /public_html
   kildekode                                 færdigbygget site                                    det kørende site

Simply.com:
/
├── public_html/          ← sitet. Erstattes ved hver import. Gem aldrig noget her selv.
├── tumlegaarden-logs/    ← log fra sitet. Oprettes automatisk, én fil pr. opstart.
├── tumlegaarden-data/    ← hemmeligheder og data. Røres aldrig af importen.
│   ├── appsettings.Production.json   (du opretter den, trin 3)
│   ├── keys/                         (oprettes af sitet: nøgler til login-cookies)
│   ├── albums/                       (oprettes af sitet: billeder fra albums)
│   └── tmp/                          (oprettes af sitet: midlertidige filer under upload)
└── tumlegaarden-shadow/  ← oprettes af IIS: kopien af appen, der rent faktisk kører
```

- Grenen **`main`** indeholder kildekoden. Her laves ændringer.
- Grenen **`release`** bygges automatisk af GitHub ved hver ændring på `main`. Den er repoets **standard-gren**, fordi Simply's import kun henter standard-grenen.
- Data og adgangskoder ligger i **`/tumlegaarden-data`**, uden for web-mappen, så en import aldrig kan slette eller udstille dem.

**Du skal bruge:** adgang til Simply.com's kontrolpanel for `tumlegaarden.dk` og til GitHub-repoet `themiggot/tumlegaarden`.

---

## 1. Tjek at webhotellet kan køre ASP.NET

Sitet er en ASP.NET Core 10-app og kræver Simply's Windows/IIS-hosting med ASP.NET. Et rent PHP/Linux-webhotel kan ikke køre det.

1. Se under **Website → Oversigt** og **Administration**, om der står ASP.NET, .NET eller Windows.
2. Er du i tvivl, så skriv til Simply's support:
   > Understøtter webhotellet for tumlegaarden.dk ASP.NET Core 10 som self-contained app, skal vi udgive til win-x64 eller win-x86, og må appen læse og skrive i mapper ved siden af public_html?
3. Sitet bygges som **32-bit (`win-x86`)**, fordi Simply's app-pool kører 32-bit. En 64-bit-pakke giver *HTTP Error 500.32 – Failed to load .NET Core host*.
   Skulle Simply en dag skifte til 64-bit, så opret repo-variablen `RUNTIME` med værdien `win-x64` under **Settings → Secrets and variables → Actions → Variables** og kør **Actions → Byg og udgiv → Run workflow**.

---

## 2. Forbered Simply.com

### 2a. HTTPS
**Website → HTTPS beskyttelse**: slå SSL-certifikat til for `tumlegaarden.dk` og `www.tumlegaarden.dk`. Sitet kræver HTTPS.

### 2b. MySQL-database
**Databaser → MySQL**: opret en database. Notér server (værtsnavn), databasenavn, brugernavn og adgangskode.
Tabellerne oprettes automatisk af sitet ved første start.

### 2c. Mailkonto til afsendelse
**E-mail**: opret `noreply@tumlegaarden.dk` med en lang, tilfældig adgangskode.
Mail fra webhotellet skal sendes via **`websmtp.simply.com`**, ikke `smtp.simply.com`, som er til mailprogrammer.
Se Simply's vejledning [Outgoing mail server for scripts](https://www.simply.com/en/support/faq/mail/23-outgoing-mail-server-for-scripts/) for port og login.

---

## 3. Opret datamappen med hemmelighederne

Hemmelighederne må **aldrig** ligge på GitHub eller i `public_html`.

1. **Website → File Manager**. Klik **Gå et niveau op**, så der står **Mappe: /** og du kan se `public_html`.
2. **Ny → Mappe**: opret `tumlegaarden-data`. Den skal ligge ved siden af `public_html`, ikke inde i den.
3. Gå ind i `tumlegaarden-data` og opret filen `appsettings.Production.json` (**Ny → Fil**, eller lav den på din computer og brug **Upload**). Indhold:

```json
{
  "Bootstrap": {
    "AdminEmail": "DIT_NAVN@tumlegaarden.dk"
  },
  "ConnectionStrings": {
    "MySql": "Server=MYSQL_SERVER;Port=3306;Database=DATABASENAVN;User Id=BRUGERNAVN;Password=ADGANGSKODE;SslMode=Required;CharSet=utf8mb4"
  },
  "Smtp": {
    "Host": "websmtp.simply.com",
    "Port": 587,
    "Security": "StartTls",
    "UserName": "noreply@tumlegaarden.dk",
    "Password": "MAILKONTOENS_ADGANGSKODE",
    "FromAddress": "noreply@tumlegaarden.dk"
  }
}
```

| Felt | Hvad det er |
|---|---|
| `Bootstrap:AdminEmail` | Den første administrator. Får en invitation, når databasen er tom. Skal være en `@tumlegaarden.dk`-adresse, du kan læse. |
| `ConnectionStrings:MySql` | Oplysningerne fra trin 2b. Indeholder adgangskoden et `;`, så kontakt udvikleren. |
| `Smtp:*` | Mailkontoen fra trin 2c. `Security` er `StartTls` (port 587), `SslOnConnect` (port 465), `StartTlsWhenAvailable` eller `None`. Brug det, Simply's vejledning angiver. |

Laver du filen på din computer, så brug en teksteditor (Notesblok, VS Code), ikke Word, og gem som UTF-8.

---

## 4. Tjek repoet på GitHub (én gang)

*Importer fra Git* kræver:
- **Repoet er offentligt.** Det er det nu. Det indeholder ingen adgangskoder, men alle kan læse koden.
- **Standard-grenen er `release`.** Tjek under **Settings → General → Default branch**, at der står `release`.
- **Seneste kørsel under Actions → Byg og udgiv er grøn.**

---

## 5. Udgiv med "Importer fra Git"

1. **Website → File Manager**, gå til `/public_html`.
2. **Slet** `index.html` (Simply's standardside), hvis den findes. **Lad** mappen `.well-known` **være**.
3. **Upload ▾ → Importer fra Git**, og udfyld:
   - **Git-URL:** `https://github.com/themiggot/tumlegaarden.git`
   - **Destinations-mappe:** `/public_html`
   - Sæt flueben i **Jeg er indforstået med virkningen af denne funktion**.
4. Klik **Importer** og vent, til den er færdig. Pakken er omkring 135 MB.
5. Tjek at `/public_html` nu indeholder `web.config`, `Tumlegaarden.Web.exe`, mappen `wwwroot` og `version.txt`.
   `version.txt` viser, hvilken commit der er udgivet.

---

## 6. Første start

1. Åbn **https://tumlegaarden.dk**. Første besøg kan tage 10–20 sekunder, mens appen starter og opretter tabellerne.
2. Du skal se login-siden med overskriften **TUMLEGAARDEN** og cookie-meddelelsen nederst.
3. Tjek at `/tumlegaarden-data` nu også indeholder mappen `keys`. Det viser, at sitet har fundet datamappen.
4. Tjek indbakken for `Bootstrap:AdminEmail`. Der ligger en mail: *"Tumlegaarden: Du er inviteret som administrator"*. Tjek også spam.
5. Åbn linket, vælg adgangskode (mindst 14 tegn, store og små bogstaver, tal og specialtegn).
6. Scan QR-koden med en authenticator-app og indtast koden.
7. **Gem de 10 gendannelseskoder** et sikkert sted, fx i en adgangskodemanager.
8. Gå til **Brugere** og invitér resten af holdet.

Ser du noget andet end login-siden, så gå til afsnit 9.

---

## 7. Kontroller at alt er i orden

- [ ] `http://tumlegaarden.dk` sender videre til `https://`.
- [ ] Ved første besøg vises pop-uppen **Privat side**, og den forsvinder efter **OK, forstået**.
- [ ] Efter login vises kalenderen, og en ny begivenhed kan oprettes og foldes ud.
- [ ] Et billede kan uploades til et album og vises bagefter. Mappen `/tumlegaarden-data/albums` er oprettet.
- [ ] Siden kan **ikke** åbnes via en udenlandsk VPN (du ser "Ingen adgang"). Tjek også loggen: `Landefilter: kun Danmark (… adresseintervaller)`.
- [ ] Mappen `/tumlegaarden-data/keys` findes.
- [ ] Administrator-invitationen kom frem, og to-faktor virker.
- [ ] En invitation til en kollega kommer frem.
- [ ] En `@gmail.com`-adresse afvises ved invitation.
- [ ] [securityheaders.com](https://securityheaders.com/?q=tumlegaarden.dk) giver karakter A eller A+.
- [ ] `https://tumlegaarden.dk/version.txt` og `https://tumlegaarden.dk/web.config` sender dig til login og viser **ikke** filen.

---

## 8. Opdatering af sitet

1. Ændringer laves på `main` på GitHub.
2. Vent til **Actions → Byg og udgiv** er grøn (2–3 minutter). Så er `release` opdateret.
3. Kør **Importer fra Git** igen som i trin 5.
4. Sitet genstarter selv med den nye version ved næste besøg. Tjek `version.txt` i File Manager.

**Databasen opgraderes selv med nye tabeller.** Kommer der en ny tabel i en ny version (som `Events` til kalenderen), oprettes den automatisk ved opstart, og loggen skriver `Oprettede manglende tabel …`. Nye *kolonner* i eksisterende tabeller oprettes ikke automatisk. Det vil stå i udgivelsesnoterne, hvis det bliver nødvendigt.

**Hvis importen fejler med låste filer:** server-softwaren understøtter ikke shadow copy. Gør så sådan:
1. Upload `app_offline.htm` fra repoets mappe `deploy/` (på `main`) til `/public_html`. Appen stopper og viser en "opdaterer"-side.
2. Kør **Importer fra Git**.
3. Slet `app_offline.htm`, hvis den stadig ligger der.

Rør aldrig `/tumlegaarden-data`. Slettes `keys`, bliver alle logget ud. Slettes `appsettings.Production.json`, kan sitet ikke nå databasen. Slettes `albums`, forsvinder alle billeder.

---

## 8a. Kun adgang fra Danmark

Sitet kan kun bruges fra danske IP-adresser. Det gælder alle sider, også for brugere der allerede er logget ind. Andre får siden **Ingen adgang** (HTTP 403).

- **Hvor listen kommer fra:** `GeoData/dk-ip-ranges.csv` i pakken. Den bygger på de regionale internetregistres data (NRO, CC BY 4.0) via ip-location-db og hentes på ny ved hvert byg på GitHub.
- **Hvad der ikke virker:** VPN med udenlandsk server, iCloud Privat Relay og mobil roaming i udlandet (trafikken går så ofte gennem en udenlandsk adresse).
- **Undtagelser:** Skal en bestemt udenlandsk adresse have adgang, så tilføj den i `/tumlegaarden-data/appsettings.Production.json`:
  ```json
  "Security": {
    "AllowedIpRanges": [ "203.0.113.7", "198.51.100.0/24" ]
  }
  ```
- **Nødstop:** Låser filteret alle ude, kan det slås fra med `"Security": { "DenmarkOnly": false }` i samme fil. Genstart app-poolen bagefter.
- **Afviste forsøg** logges med IP-adresse i `/tumlegaarden-logs`.

---

## 9. Fejlsøgning

**Loggen er altid slået til.** Den ligger i `/tumlegaarden-logs` ved siden af `public_html`, med én fil pr. opstart (`stdout_ÅÅÅÅMMDDTTMMSS_….log`). Den nyeste fil er den interessante.
Se også **Website → Webserver logs**. Slet gamle filer i `/tumlegaarden-logs` en gang imellem.

De første linjer i loggen viser:
- `Datamappe: …`: mappen, sitet læser hemmeligheder fra.
- `SMTP: server:port (kryptering), login: …, adgangskode: sat/mangler, afsender: …`: de mailindstillinger, sitet faktisk bruger. Adgangskoden skrives aldrig.

| Symptom | Sandsynlig årsag | Løsning |
|---|---|---|
| Du ser indholdet af `web.config`, en mappeoversigt, 403 eller PHP-fejl | Webhotellet kører ikke ASP.NET | Se trin 1. Kontakt Simply. |
| Importen fejler: "repository not found" eller lignende | Repoet er privat, eller URL'en er forkert | Se trin 4. URL'en skal ende på `.git`. |
| Importen henter kildekode (`src/`, `README.md`) i stedet for `web.config` | Standard-grenen er `main`, ikke `release` | Sæt **Default branch** til `release` (trin 4) og importér igen. |
| **HTTP 500.19** | IIS forstår ikke en linje i `web.config` | Prøv at fjerne `removeServerHeader="true"`. Rettelsen skal laves i `src/Tumlegaarden.Web/web.config` på `main`. Kontakt udvikleren. |
| **HTTP 500.32** – Failed to load .NET Core host | Pakken er bygget til en anden bitness end app-poolen | Tjek `version.txt`: der skal stå `Runtime: win-x86`. Se trin 1. |
| **HTTP 500.30** | Appen starter, men går ned under opstart | Læs loggen. Oftest database eller konfiguration (se nedenfor). |
| 500-fejl, der nævner shadow copy, eller `tumlegaarden-shadow` kan ikke oprettes | IIS må ikke skrive ved siden af `public_html` | Opret mappen `/tumlegaarden-shadow` selv i File Manager. Hjælper det ikke, så bed udvikleren slå shadow copy fra og brug `app_offline.htm` ved opdateringer (afsnit 8). |
| Loggen siger "Datamappen ligger inde i web-mappen" | `/tumlegaarden-data` findes ikke eller ligger forkert | Tjek at mappen ligger ved siden af `public_html` og er stavet præcis sådan (trin 3). |
| **HTTP 500.30** med databasefejl i loggen | Forkert forbindelsesstreng | Tjek server, bruger og adgangskode. Ved SSL-fejl: skift `SslMode=Required` til `SslMode=Preferred`. |
| **HTTP 400 Bad Request – Invalid Hostname** | Sitet tilgås via en adresse, der ikke står i `AllowedHosts` | Tilføj adressen i `AllowedHosts` i `appsettings.Production.json`, fx `"AllowedHosts": "tumlegaarden.dk;www.tumlegaarden.dk;ny.adresse.dk"`. |
| Ingen invitationsmail | SMTP-oplysninger forkerte, eller mailen er i spam | Læs loggen ("Kunne ikke sende"). Tjek trin 2c og 3. |
| Kom aldrig nogen administrator-invitation | Se afsnittet "Ingen administrator-invitation" nedenfor | |
| Alle bliver logget ud efter hver opdatering | `keys` ligger i `public_html` i stedet for `/tumlegaarden-data` | Se rækken om datamappen ovenfor. |
| "Ingen adgang – kun danske IP-adresser", men du er i Danmark | VPN, iCloud Privat Relay, eller adressen mangler i listen | Slå VPN/Privat Relay fra. Hjælper det ikke, så tilføj din adresse (står på siden) under `AllowedIpRanges` (afsnit 8a). |
| Alle får "Ingen adgang" | Listen over danske adresser mangler i pakken | Loggen siger `Listen over danske IP-adresser mangler`. Brug nødstoppet i afsnit 8a, og giv udvikleren besked. |
| Loggen advarer om "lokal/privat IP" | Sitet står bag en proxy hos Simply, så landefilteret ser proxyens adresse | Filteret lukker så alle igennem. Giv udvikleren besked, så den rigtige klientadresse kan læses fra proxyen. |
| Upload af billeder fejler med **HTTP 413** eller "404.13" | Forespørgslen er større end IIS tillader | `web.config` tillader 200 MB under `/Albums`. Upload færre billeder ad gangen. Hjælper det ikke, så spørg Simply, om de har en lavere grænse. |
| Upload fejler, og loggen nævner `tmp` eller adgang nægtet | Sitet kan ikke skrive i `/tumlegaarden-data/tmp` eller `albums` | Tjek at app-poolen har skriveadgang til `/tumlegaarden-data` (den kan skrive `keys`, så det burde virke). |
| Pladsen på webhotellet løber tør | Billederne fylder | Albumsiden viser, hvor meget billederne bruger. Slet gamle albums, eller udvid pladsen hos Simply. |
| "For mange forsøg" | Rate limiting: maks. 30 loginforsøg pr. minut pr. IP | Vent et minut. |
| Brugeren har mistet telefon og gendannelseskoder | | En administrator vælger **Nulstil 2FA** under Brugere. |


### Ingen administrator-invitation

Invitationen sendes kun, når der **ingen brugere** findes. Den går til adressen i `Bootstrap:AdminEmail`, ikke automatisk til admin@.

1. **Tjek `/tumlegaarden-data`:** Findes mappen `keys` derinde? Hvis ikke, har sitet ikke fundet datamappen og har ikke læst `appsettings.Production.json`. Tjek mappens navn og placering (ved siden af `public_html`).
   Ligger der i stedet en mappe `App_Data` inde i `/public_html`, er det samme problem.
2. **Tjek spam** i den indbakke, `Bootstrap:AdminEmail` peger på.
3. **Genstart app-poolen** og læs den nyeste fil i `/tumlegaarden-logs`. Tjek linjen `SMTP: …` (skal være `websmtp.simply.com`). Loggen siger én af disse:
   - `Administrator-invitation sendt til …`: mailen er afsendt. Tjek spam, og at adressen findes.
   - `Kunne ikke sende administrator-invitation …`: SMTP-indstillingerne er forkerte. Linjen under viser fejlen, fx `Name or service not known` (forkert server), `Authentication failed` (forkert login) eller en TLS-fejl (ret `Security`). Ret dem i `appsettings.Production.json`. Der prøves igen ved næste start.
   - `… blev sendt … Sendes igen ved en genstart efter 15 minutter`: en invitation er sendt for nylig. Vent 15 minutter og genstart app-poolen for at få en ny.
   - `Ingen brugere og ingen gyldig Bootstrap:AdminEmail`: adressen mangler eller er ikke en @tumlegaarden.dk-adresse.
4. Ændrer du `Bootstrap:AdminEmail`, sendes en ny invitation til den nye adresse ved næste genstart. Den gamle bliver ugyldig.

---

## 10. Alternativ: upload med FTP

Brug denne metode, hvis *Importer fra Git* ikke virker, eller repoet skal være privat.

1. Hent pakken: GitHub → **Actions → Byg og udgiv** → seneste grønne kørsel → **Artifacts** → `tumlegaarden-win-x86`. Pak zip-filen ud.
2. Forbind med [FileZilla](https://filezilla-project.org/) eller [WinSCP](https://winscp.net/) med oplysningerne fra **File Manager → Loginoplysninger**. Brug SFTP eller **Kræv eksplicit FTP over TLS**, aldrig ukrypteret FTP.
3. Ved opdatering: upload `deploy/app_offline.htm` til `/public_html` først.
4. Upload **indholdet** af den udpakkede mappe til `/public_html` og overskriv.
5. Slet `app_offline.htm`.

Trin 2–3 (Simply-forberedelse og datamappe) gælder også her.

---

## 11. Backup og sikkerhed

- **Database:** Tag jævnligt backup af MySQL-databasen via kontrolpanelet. Se også **Website → Genskab data**.
- **`/tumlegaarden-data`:** Tag backup af hele mappen sammen med databasen. Den indeholder også alle billeder fra albums (`albums/`), og de findes ingen andre steder.
- **Hemmeligheder:** `appsettings.Production.json` findes kun i `/tumlegaarden-data` og hos dig. Læg den aldrig på GitHub eller i mails. Skift adgangskoderne, hvis den slipper ud.
- **Repoet er offentligt.** Læg aldrig adgangskoder, forbindelsesstrenge eller persondata i repoet. GitHub Actions tjekker, at `appsettings.Production.json` ikke kommer med i pakken.
- **Administratorer:** Hav altid mindst to administratorer, så en mistet telefon ikke låser alle ude.
