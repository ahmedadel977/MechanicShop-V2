# 10. الأمان

- `.env` مستبعد من Git ومن سياق Docker عبر `.gitignore` و`.dockerignore`. لا تعرض قيمه في terminal أو CI logs، ولا تضع أسرارًا في Dockerfile أو image layers.
- ملف `docker-compose/.env.example` يحتوي أسماء المتغيرات فقط وقيَمًا فارغة للسر وقبول الترخيص. راجع تراخيص Microsoft SQL Server وSeq قبل كتابة `Y`.
- استخدم حساب runtime غير root (`$APP_UID`) و`cap_drop: ALL` و`no-new-privileges` لخدمة API.
- منافذ SQL وRedis وSeq وPrometheus وGrafana مكشوفة على المضيف عبر ملف التطوير override فقط. في الإنتاج حدّد الوصول عبر شبكة خاصة وproxy/TLS ومصادقة.
- لا تستخدم SQL Server Developer في الإنتاج. لا تستخدم كلمات مرور افتراضية أو `SEQ_FIRSTRUN_NOAUTHENTICATION`.
- الصور مثبتة على tags محددة لتكرارية البناء؛ راجع تحديثات الأمان بعملية تحديث واعتماد دورية.
- البيئة الحالية تمرر أسرار التشغيل كمتغيرات حاوية لأغراض تطوير محلي. في الإنتاج استخدم secret manager أو Docker/Kubernetes secrets، ودوّر الأسرار، وطبّق أقل صلاحية.
- صحة `/health` تشمل الاعتماد على SQL وRedis؛ تأكد من عدم نشر تفاصيل داخلية لاستجابة الصحة عبر proxy عام.
