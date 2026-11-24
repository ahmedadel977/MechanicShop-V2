# 6. Volumes والبيانات الدائمة

الحاويات قابلة للاستبدال، لذلك تحفظ البيانات المهمة في Docker named volumes:

| Volume | البيانات |
| --- | --- |
| `mechanicshop_sqlserver-data` | ملفات SQL Server. |
| `mechanicshop_redis-data` | سجل Redis AOF. |
| `mechanicshop_seq-data` | بيانات Seq. |
| `mechanicshop_prometheus-data` | قاعدة مقاييس Prometheus لمدة افتراضية 15 يومًا. |
| `mechanicshop_grafana-data` | إعدادات Grafana وبياناته. |

الـnamed volume مستقل عن عمر الحاوية. إعدادات Collector وPrometheus وGrafana ولوحة التحكم تُحمّل من ملفات المشروع read-only، ولذلك تُراجع في Git. أما volume فهو ليس نسخة احتياطية؛ الإنتاج يحتاج نسخًا احتياطية وخطة استعادة واختبارًا دوريًا.

`docker compose down` يحذف الحاويات والشبكة ويترك الـvolumes. `docker compose down --volumes` يحذفها ويمسح البيانات؛ استخدمه فقط إذا كنت تقصد إعادة ضبط كاملة.
