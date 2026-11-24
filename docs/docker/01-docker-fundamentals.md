# 1. مفاهيم Docker

## الصورة والحاوية

الصورة قالب غير متغير يحتوي نظام التشغيل الأدنى وملفات التطبيق ومشغّل .NET. الحاوية عملية تعمل من صورة، لها نظام ملفات مؤقت وشبكة وإعدادات مستقلة. حذف الحاوية لا يحذف البيانات الموجودة في volume مستقل.

في هذا المشروع ينتج `src/MechanicShop.Api/Dockerfile` صورة API. يقرأ Docker ملفات المصدر من جذر الحل لأن مشاريع API تعتمد على Contracts وApplication وClient وInfrastructure.

## Docker Engine وCLI

`docker` و`docker compose` عميلان يرسلان أوامر إلى Docker Engine. وجود CLI لا يعني أن المحرك يعمل؛ تحقق بـ`docker info`. في Windows يستخدم Docker Desktop عادة محرك Linux عبر named pipe، ولذلك يلزم تشغيله وصلاحية المستخدم للاتصال به.

## Compose

Compose يصف خدمات مترابطة في YAML. هنا يربط API بـSQL Server وRedis وSeq وOpenTelemetry Collector وPrometheus وGrafana، وينشئ شبكة داخلية وnamed volumes. اسم الخدمة يصبح اسم DNS داخليًا.

## السياق

سياق البناء هو الملفات التي يسمح Docker بقراءتها أثناء `docker build`. من CLI هو `.` عند تشغيل الأمر من جذر الحل؛ في Compose هو `..` من مجلد `docker-compose/`. في الحالتين السياق الفعلي هو جذر الحل، ولذلك يقرأ Docker ملف `.dockerignore` الموجود في جذر الحل، وليس الملف التوضيحي داخل مجلد Compose.
