using System;
using KzBsv;

namespace KzPaymailAsp
{
    public class KzPaymailClientInfo
    {
        string _h;
        string _b58;
        string _kp;
        int _n;
        KzExtPubKey _epk;
        KzPubKey _pk;

        public string Paymail => _h;
        public string ExtendedPublicKey => _b58;
        public string KeyPath => _kp;
        public int NextDerivation => _n;

        public KzExtPubKey Epk => ValidateExtPubKey();
        public KzPubKey Pk => ValidatePubKey();

        KzExtPubKey ValidateExtPubKey()
        {
            if (_epk == null) {
                _epk = KzB58ExtPubKey.GetKey(_b58);
                if (_kp != null)
                    _epk = _epk.Derive(_kp);
            }

            return _epk;
        }

        KzPubKey ValidatePubKey()
        {
            if (_pk == null) {
                _pk = DeriveNext(int.MaxValue, -1).k;
            }
            return _pk;
        }

        (KzPubKey k, int n) DeriveNext(int n, int offset = 1)
        {
            do {
                var cepk = Epk.Derive(n);
                if (cepk != null)
                    return (cepk.PubKey, n);
                var nl = (long)n + offset;
                if (nl < 0 || nl > int.MaxValue)
                    throw new InvalidOperationException("Extended public key is exhausted.");
                n = (int)nl;
            } while (true);
        }

        public KzPubKey DeriveNext()
        {
            ValidatePubKey();
            KzPubKey cpk;
            (cpk, _n) = DeriveNext(_n);
            _n++;
            return cpk;
        }

        public KzPaymailClientInfo(string h, string epk, string kp = null)
        {
            _h = h;
            _b58 = epk;
            _kp = kp;
            _n = 0;
        }
    }
}
