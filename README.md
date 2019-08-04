# KzPaymailAsp

.Net ASP implementation of Paymail server protocol.

Based on the [KzBsv library](https://github.com/kzbsv/KzBsv/tree/master/KzBsv) which includes a Paymail Client protocol implementation.

KzPaymailAsp is a simple Azure App Service that implements the Paymail server protocol
described on [bsvalias.org](http://bsvalias.org).

No code customization is required to test but this code is intended as a starting point, not a complete, secure solution.
Specifically, the following should be addressed:

* Addresses are reused each time the service is restarted. This simplifies the code by not requiring database functionality.
* DNSSEC must be implemented and required of senders.

Configuration can be done entirely through the [Azure Portal)[https://portal.azure.com].

After publishing the KzPaymailAsp web application to Azure, add paymail clients as follows:

* Add an Application settings for each paymail client. Settings -> Configuration -> Application settings.
* The name of each client should follow the pattern KzPaymailClientN where N starts at zero and increments by one.
* The value of each setting must have the following format:

      kzpaymailasp@kzbsv.org,0,xpub661MyMwAqRbcFwcmpFH8Kd8hAJxCSQMkLuyYhhy4d1VWiiJ4DUy2pwQG71LKspkqvyiqDyxt8vn1GUTVrcTQhTom3tdMTXTiYCj5L6q6gfU

* Three comma separated values:
  * The paymail identity in e-mail address format.
  * Derivation path from xpub.
  * The xpub from which to derive addresses.

Optionally uses the Azure SendGrid e-mail service to log service requests. There is no fee to create a SendGrid account for development support.
Obtain an API key and add it as Application setting KzSendGridKey.




