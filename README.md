# DropLink Native

DropLink Native è un'applicazione Windows per condividere file, realizzata con **WPF e .NET 10**. L'interfaccia desktop usa controlli Windows nativi e non include WebView. Il destinatario usa il browser per vedere la pagina di download; i dati del file vengono trasmessi dal PC mittente.

> **Versione 0.4.7 — feedback nativo delle operazioni.** Questo archivio contiene il progetto sorgente e gli script, non un `DropLink.exe` già compilato. Esegui build e test su Windows prima di affidarti all'app. DropLink scarica il client ufficiale `zrok2` al primo utilizzo, ne verifica il checksum e gestisce la prima autorizzazione in una finestra nativa.

### Novità 0.4.7

- Rimossa la freccia decorativa in basso a destra dell'illustrazione principale.
- Aggiunti toast nativi WPF in-app per copia link, creazione/revoca condivisioni, apertura browser, salvataggio impostazioni, verifica DNS e attivazione/arresto del tunnel.
- Gli errori di routine vengono mostrati nell'interfaccia senza interrompere il flusso con finestre modali; restano modali solo le conferme e le configurazioni che richiedono input.
- Aggiornate le note per la pubblicazione su GitHub; la pipeline Windows esistente esegue test e pubblica un artefatto win-x64.

### Novità 0.4.6

- Riconosce anche i domini pubblici zrok stampati senza `https://` o all’interno dei log JSON e li normalizza a HTTPS.
- Aggiunti test di regressione per URL, hostname, log JSON e domini non validi.
- Rimossa la scritta tecnica in fondo alla schermata Condivisioni.

### Novità 0.4.5

- Riconosce il messaggio del client zrok `unable to load environment; did you 'zrok2 enable'?` e apre la finestra di autorizzazione dentro DropLink invece di mostrare un errore generico. Aggiunti test di regressione.

### Novità 0.4.2

- Download automatico e verifica SHA-256 del client `zrok2` ufficiale, senza installazione manuale o comandi PowerShell.
- Prima autorizzazione guidata nella finestra DropLink; nelle sessioni successive basta un clic per avviare il tunnel.
- I link delle condivisioni già presenti vengono aggiornati quando il tunnel pubblico è attivo e ripristinati quando viene arrestato.

## Funzionalità

- Interfaccia Windows nativa, senza Tauri e senza WebView.
- Lingue: italiano, inglese, spagnolo, francese, tedesco, portoghese, cinese e giapponese.
- Selezione di un file, token casuale crittografico a 128 bit, copia/apertura/revoca del link.
- Server Kestrel incorporato: i file restano sul PC mittente e non vengono salvati su un cloud. La modalità LAN è diretta; nella modalità Internet temporanea il traffico viene inoltrato attraverso il tunnel zrok.
- Supporto `HEAD`, richieste di singoli intervalli di byte, nomi Unicode e buffer da 64 KiB.
- Senza dominio pubblico i link usano HTTP nella rete locale. Con un hostname pubblico configurato, i nuovi link usano `https://` sulla porta 443 tramite Caddy.
- Verifica DNS e script PowerShell per diagnosi della rete.
- Link Internet HTTPS temporanei tramite `zrok2`, avviabili/arrestabili dalla schermata Condivisioni senza port forwarding.
- Impostazioni in `%APPDATA%\DropLink\settings.json`.


## Link Internet temporaneo (zrok2)

Per creare un URL HTTPS temporaneo anche dietro CGNAT, senza aprire porte sul router:

1. Avvia DropLink e premi **Attiva link Internet temporaneo**. Al primo utilizzo l'app scarica da GitHub il client Windows ufficiale di zrok e ne verifica il checksum SHA-256. Non serve installare `zrok2` a mano.
2. La prima volta DropLink apre una finestra per la configurazione personale. Crea o accedi a un account gratuito su <https://myzrok.io/>, copia il token personale dalla pagina dell'account e incollalo nella finestra DropLink. L'autorizzazione viene eseguita dall'app; non è necessario aprire PowerShell.
3. Dopo la prima autorizzazione, il pulsante avvia il tunnel e crea un URL HTTPS temporaneo. Le condivisioni già presenti vengono aggiornate per usare l'URL pubblico; copia il link mostrato in DropLink e invialo al destinatario.
4. Mantieni DropLink aperto mentre il destinatario scarica il file. Premi **Disattiva link Internet** o chiudi l'app per terminare il tunnel.

Il token è personale: non inserirlo nei file GitHub né condividere quello di un altro account. DropLink non memorizza il token nei propri file; zrok conserva la sua configurazione nel profilo dell'utente Windows. Un token personale è necessario perché il servizio ospitato zrok richiede l'autorizzazione di ogni account: il repository non include e non condivide credenziali comuni.

I byte del file vengono letti dal PC mittente, ma il traffico Internet passa attraverso zrok: è quindi un tunnel, non un collegamento diretto garantito. Possono esserci latenza, overhead, quote e una pagina interstitial del provider; verifica le condizioni aggiornate su <https://zrok.io/pricing/>. Maggiori dettagli: `docs/TEMPORARY_INTERNET_SHARING.md`.

## Configurare i link pubblici HTTPS

Il server interno di DropLink usa HTTP sulla porta locale (predefinita `43821`). Caddy gestisce TLS sulle porte pubbliche TCP 443 e reindirizza HTTP dalla porta 80 a HTTPS, inoltrando le richieste a DropLink sullo stesso PC. Il file non transita attraverso un servizio cloud o un relay VPS.

1. Procurati un dominio/sottodominio e configura il record DNS `A`/`AAAA` o un DDNS verso la tua connessione Internet.
2. Esegui lo script diagnostico:

   ```powershell
   .\tools\Test-DropLinkNetwork.ps1
   ```

   Confronta l'IPv4 rilevato con l'IPv4 WAN/Internet mostrato dalla pagina di stato del router. Un indirizzo WAN privato/condiviso o diverso può indicare doppio NAT o CGNAT; la diagnosi non è conclusiva.
3. Installa Caddy dalla [pagina ufficiale di download](https://caddyserver.com/download). Metti `caddy.exe` nel `PATH` oppure in `deployment/caddy/`.
4. Apri PowerShell **come amministratore** ed esegui:

   ```powershell
   .\deployment\caddy\Enable-CaddyFirewall.ps1
   ```

5. Nel router assegna al PC un IP LAN stabile e inoltra TCP esterna `80` verso TCP `80` del PC e TCP esterna `443` verso TCP `443` del PC. **Non inoltrare direttamente su Internet la porta interna di DropLink (`43821` per impostazione predefinita).**
6. Avvia DropLink, poi dalla cartella principale del progetto esegui (sostituisci dominio e porta locale):

   ```powershell
   .\deployment\caddy\Start-PublicHttps.ps1 -Domain "share.example.com" -ServerPort 43821
   ```

   Tieni aperta la console di Caddy mentre usi i link pubblici. Caddy valida la configurazione e richiede/rinnova automaticamente un certificato pubblico quando DNS e porte sono raggiungibili. Dettagli: [Caddy automatic HTTPS](https://caddyserver.com/docs/automatic-https).
7. In DropLink, apri **Impostazioni**, inserisci lo stesso hostname e salva. Poi crea nuove condivisioni: i link pubblici saranno del tipo `https://share.example.com/s/<token>`. I link già creati non cambiano URL; revocali e ricreali se necessario.
8. Verifica il link da un telefono con Wi-Fi disattivato (rete mobile). La sola risoluzione DNS non dimostra che router, firewall e connessione siano raggiungibili dall'esterno.

### Limite della connessione

Se l'operatore usa CGNAT e non offre connettività in ingresso, il normale port forwarding non renderà raggiungibile il server. Chiedi all'operatore un IPv4 pubblico o una configurazione IPv6 utilizzabile. Un tunnel o relay VPS può essere un'alternativa, ma cambierebbe il percorso e non sarebbe più un trasferimento strettamente diretto.

## Requisiti, build e test

- Windows 11 x64
- SDK .NET 10
- Connessione Internet per il primo ripristino dei pacchetti di test

SDK: <https://dotnet.microsoft.com/download/dotnet/10.0>. `NuGet.Config` usa il feed ufficiale nuget.org per le dipendenze dei test.

```powershell
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release
dotnet run --project .\src\DropLink\DropLink.csproj
```

Per pubblicare una versione autonoma Windows x64:

```powershell
dotnet publish .\src\DropLink\DropLink.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64
```

L'output deve contenere `artifacts\win-x64\DropLink.exe`. L'archivio sorgente non include un eseguibile precompilato.

## Sicurezza

- I link sono credenziali al portatore: chiunque li possieda può scaricare il file associato finché il link non viene revocato o DropLink non viene chiuso.
- Usa link pubblici `https://` solo dopo che Caddy ha ottenuto un certificato attendibile e dopo un test dall'esterno.
- I link locali HTTP non sono cifrati; evita di usarli per contenuti riservati su reti non fidate.
- Non inoltrare la porta interna di DropLink dal router: esponi solo Caddy sulle porte TCP 80 e 443.
- Non sono inclusi telemetria, password sui link, scadenza automatica o storico persistente dei trasferimenti.

Leggi `docs/NETWORKING.md`, `docs/SECURITY.md` e `docs/TESTING.md`.

## Licenza

MIT. Vedi `LICENSE`.
