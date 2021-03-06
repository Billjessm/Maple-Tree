#ifndef TICKET_H
#define TICKET_H

#include <QObject>
#include "titleinfo.h"

class Ticket : public QObject {
  Q_OBJECT
 public:
  explicit Ticket(QObject* parent = nullptr);

  static void Create(TitleInfo* ti);

  QByteArray data;
  qulonglong len;

 signals:

 public slots:
};

#endif  // TICKET_H
