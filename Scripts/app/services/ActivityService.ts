﻿/// <reference path="../_all.ts" />

module dockyard.services {

    export interface IActivityService {
        save: (activity: interfaces.IActivityDTO) => ng.IPromise<interfaces.IActivityDTO>;
        configure: (activity: interfaces.IActivityDTO) => ng.IPromise<interfaces.IActivityDTO>;
    }

    interface IActivityFunction {
        (activityDTO: interfaces.IActivityDTO): ng.IPromise<interfaces.IActivityDTO>;
    }

    class ActivityRequestQueue {
        constructor(deferred: ng.IDeferred<interfaces.IActivityDTO>, opFunction: IActivityFunction, activityDTO: interfaces.IActivityDTO) {
            this.deferred = deferred;
            this.opFunction = opFunction;
            this.activityDTO = activityDTO;
        }
        public deferred: ng.IDeferred<interfaces.IActivityDTO>;
        public opFunction: IActivityFunction;
        public activityDTO: interfaces.IActivityDTO;
    }
    
    ///This service queues request for same activity
    //therefore no concurrent request of the same activity can be made
    class ActivityService implements IActivityService {

        private activityRequestMap: { [id: string]: Array<ActivityRequestQueue>; } = {};
        
        constructor(private $http: ng.IHttpService, private $q: ng.IQService) {

        }

        private processNext(id: string) {
            //end of queue
            if (this.activityRequestMap[id].length === 0) {
                return;
            }
            var queueElement = this.activityRequestMap[id][0];
            var operationPromise = <ng.IPromise<interfaces.IActivityDTO>>queueElement.opFunction.call(this, queueElement.activityDTO);
            operationPromise.then((activityDTO: interfaces.IActivityDTO) => {
                queueElement.deferred.resolve(activityDTO);
            }, (err) => {
                queueElement.deferred.reject(err);
            }).finally(() => {
                //remove processed element from queue
                this.activityRequestMap[id].shift();
                this.processNext(id);
            });
        }

        private queueRequest(opFunction: IActivityFunction, activityDTO: interfaces.IActivityDTO): ng.IPromise<interfaces.IActivityDTO> {
            var deferred = this.$q.defer<interfaces.IActivityDTO>();

            if (!this.activityRequestMap[activityDTO.id]) {
                this.activityRequestMap[activityDTO.id] = [];
            }
            //push this operation to queue
            this.activityRequestMap[activityDTO.id].push(new ActivityRequestQueue(deferred, opFunction, activityDTO));
            //process this immediately
            if (this.activityRequestMap[activityDTO.id].length === 1) {
                this.processNext(activityDTO.id);
            }
            return deferred.promise;
        }

        private saveInternal(activityDTO: interfaces.IActivityDTO): ng.IPromise<interfaces.IActivityDTO> {
            return this.$http.post('/api/activities/save', activityDTO).then((resp) => resp.data);
        }

        private configureInternal(activityDTO: interfaces.IActivityDTO): ng.IPromise<interfaces.IActivityDTO> {
            return this.$http.post('/api/activities/configure', activityDTO).then((resp) => resp.data);
        }

        public save(activityDTO: model.ActivityDTO): ng.IPromise<interfaces.IActivityDTO> {
            return this.queueRequest(this.saveInternal, activityDTO);
        }

        public configure(activityDTO: model.ActivityDTO): ng.IPromise<interfaces.IActivityDTO> {
            return this.queueRequest(this.configureInternal, activityDTO);
        }
    }

    app.factory('ActivityService', ['$http', '$q', ($http: ng.IHttpService, $q: ng.IQService): IActivityService => new ActivityService($http, $q)]);
}